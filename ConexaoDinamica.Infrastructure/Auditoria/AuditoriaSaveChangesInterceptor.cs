using System.Globalization;
using System.Reflection;
using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Captura adições, alterações e remoções e as transforma em eventos de auditoria.
    ///
    /// ── A armadilha de timing (o ponto central desta classe) ──────────────────
    /// O interceptor tem dois momentos possíveis, e NENHUM deles serve sozinho:
    ///
    ///   SavingChanges  -> o ChangeTracker ainda tem OriginalValues e CurrentValues,
    ///                     mas entidades novas AINDA NÃO TÊM ID: ele é gerado pelo
    ///                     banco no INSERT, então lê-se um temporário negativo.
    ///
    ///   SavedChanges   -> os IDs já existem, mas o ChangeTracker já aceitou as
    ///                     mudanças: os estados viraram Unchanged e OriginalValues
    ///                     passou a ser igual a CurrentValues. O diff sumiu.
    ///
    /// Daí a divisão de trabalho: SavingChanges COLETA (valores originais, diffs),
    /// SavedChanges RESOLVE (chaves reais) e publica. Vale tanto para a raiz quanto
    /// para as partes do agregado — por isso o caminho do diff de uma parte
    /// ("Itens[7].Quantidade") só é montado na segunda fase: antes disso, o 7 ainda
    /// seria -2147482645.
    ///
    /// ── Escopo ───────────────────────────────────────────────────────────────
    /// Só agregados raiz geram evento. Partes entram no evento da sua raiz;
    /// entidades não marcadas são ignoradas.
    ///
    /// ── Por que scoped ───────────────────────────────────────────────────────
    /// A classe guarda estado entre as duas chamadas. Como singleton, requisições
    /// simultâneas embaralhariam os eventos umas das outras.
    /// </summary>
    public class AuditoriaSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IAuditoriaRepository _auditoriaRepository;
        private readonly IContextoAuditoria _contexto;

        private readonly List<EventoPendente> _pendentes = [];

        public AuditoriaSaveChangesInterceptor(
            IAuditoriaRepository auditoriaRepository,
            IContextoAuditoria contexto)
        {
            _auditoriaRepository = auditoriaRepository;
            _contexto = contexto;
        }

        /// <summary>Parte de agregado coletada, à espera da chave definitiva.</summary>
        private sealed record PartePendente(
            EntityEntry Entry,
            string NomeColecao,
            EntityState Estado,
            Dictionary<string, object?> Snapshot,
            List<AlteracaoCampo> Alteracoes);

        private sealed record EventoPendente(
            EventoAuditoria Evento,
            EntityEntry Raiz,
            List<PartePendente> Partes);

        // ── Fase 1: coleta ────────────────────────────────────────────────────

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _pendentes.Clear();

            if (eventData.Context is null)
                return ValueTask.FromResult(result);

            var usuario = _contexto.ObterUsuario();
            var origem = _contexto.ObterOrigem();
            var correlationId = _contexto.ObterCorrelationId();

            var entries = eventData.Context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            var partesPorRaiz = AgruparPartesPorRaiz(entries);

            foreach (var entry in entries)
            {
                if (entry.Entity is not IAuditavelRaiz)
                    continue;

                var tipoEvento = entry.State switch
                {
                    EntityState.Added => TipoEventoAuditoria.Adicao,
                    EntityState.Modified => TipoEventoAuditoria.Alteracao,
                    EntityState.Deleted => TipoEventoAuditoria.Remocao,
                    _ => (TipoEventoAuditoria?)null
                };

                if (tipoEvento is null)
                    continue;

                var evento = new EventoAuditoria
                {
                    TipoEvento = tipoEvento.Value,
                    CorrelationId = correlationId,
                    Usuario = usuario,
                    Origem = origem,
                    Entidade = new EntidadeAuditada { Tipo = entry.Entity.GetType().Name },
                    Snapshot = MontarSnapshot(entry, tipoEvento.Value),
                    Alteracoes = tipoEvento == TipoEventoAuditoria.Alteracao ? MontarDiff(entry) : []
                };

                var partes = partesPorRaiz.TryGetValue(entry.Entity, out var lista)
                    ? lista.Select(CriarPartePendente).ToList()
                    : [];

                _pendentes.Add(new EventoPendente(evento, entry, partes));
            }

            return ValueTask.FromResult(result);
        }

        /// <summary>
        /// Coleta o que só existe agora: snapshot e diff da parte. O caminho
        /// qualificado ("Itens[7].Quantidade") fica para a fase seguinte, quando a
        /// chave real estiver disponível.
        /// </summary>
        private static PartePendente CriarPartePendente(EntityEntry parte)
        {
            var tipoParte = parte.State switch
            {
                EntityState.Added => TipoEventoAuditoria.Adicao,
                EntityState.Deleted => TipoEventoAuditoria.Remocao,
                _ => TipoEventoAuditoria.Alteracao
            };

            return new PartePendente(
                parte,
                NomeDaColecao(parte),
                parte.State,
                MontarSnapshot(parte, tipoParte),
                parte.State == EntityState.Modified ? MontarDiff(parte) : []);
        }

        // ── Fase 2: resolução e publicação ────────────────────────────────────

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (_pendentes.Count == 0)
                return result;

            foreach (var pendente in _pendentes)
            {
                var evento = pendente.Evento;

                evento.Entidade.Id = ObterChave(pendente.Raiz);
                CorrigirChavesNoSnapshot(pendente.Raiz, evento.Snapshot);

                IncorporarPartes(pendente, evento);

                await DesnormalizarReferenciasAsync(pendente.Raiz, evento, cancellationToken);
            }

            var eventos = _pendentes.Select(p => p.Evento).ToList();
            _pendentes.Clear();

            // Aguardado, e não disparado em segundo plano: sem o await, o escopo do
            // DI poderia ser descartado com a gravação ainda em curso. O repositório
            // não propaga exceções — a falha vira log e a operação de negócio segue.
            await _auditoriaRepository.RegistrarAsync(eventos, cancellationToken);

            return result;
        }

        /// <summary>
        /// O snapshot foi montado enquanto uma entidade nova ainda usava a chave
        /// temporária negativa do EF. Sem esta correção, o documento gravado
        /// carregaria valores como -2147482647 no lugar do id real.
        /// </summary>
        private static void CorrigirChavesNoSnapshot(EntityEntry entry, Dictionary<string, object?> snapshot)
        {
            foreach (var chave in entry.Properties.Where(p => p.Metadata.IsPrimaryKey() || p.Metadata.IsForeignKey()))
            {
                if (snapshot.ContainsKey(chave.Metadata.Name))
                    snapshot[chave.Metadata.Name] = Normalizar(chave.CurrentValue);
            }
        }

        /// <summary>
        /// Encaixa as partes no evento da raiz, agora com as chaves definitivas.
        ///
        /// Os snapshots vão para Partes, agrupados pelo nome da navegação ("Itens").
        /// As mudanças entram no diff da raiz com caminho qualificado
        /// ("Itens[7].Quantidade: 2 -> 5"), para que quem lê saiba exatamente qual
        /// item mudou sem precisar abrir outro evento.
        /// </summary>
        private static void IncorporarPartes(EventoPendente pendente, EventoAuditoria evento)
        {
            foreach (var grupo in pendente.Partes.GroupBy(p => p.NomeColecao))
            {
                var itens = new List<Dictionary<string, object?>>();

                foreach (var parte in grupo)
                {
                    CorrigirChavesNoSnapshot(parte.Entry, parte.Snapshot);

                    // Removida não entra na lista: ela deixou de fazer parte do
                    // agregado. A saída fica registrada no diff.
                    if (parte.Estado != EntityState.Deleted)
                        itens.Add(parte.Snapshot);

                    var identificador = ObterChave(parte.Entry);

                    switch (parte.Estado)
                    {
                        // Entrada e saída de parte são mudanças DO AGREGADO, e
                        // precisam aparecer no diff mesmo quando nenhum campo da
                        // raiz mudou.
                        case EntityState.Added:
                            evento.Alteracoes.Add(new AlteracaoCampo
                            {
                                Campo = $"{parte.NomeColecao}[{identificador}]",
                                De = null,
                                Para = "adicionado"
                            });
                            break;

                        case EntityState.Deleted:
                            evento.Alteracoes.Add(new AlteracaoCampo
                            {
                                Campo = $"{parte.NomeColecao}[{identificador}]",
                                De = "existente",
                                Para = "removido"
                            });
                            break;

                        default:
                            foreach (var alteracao in parte.Alteracoes)
                            {
                                alteracao.Campo = $"{parte.NomeColecao}[{identificador}].{alteracao.Campo}";
                                evento.Alteracoes.Add(alteracao);
                            }
                            break;
                    }
                }

                if (itens.Count > 0)
                    evento.Partes[grupo.Key] = itens;
            }
        }

        // ── Descoberta de agregados ───────────────────────────────────────────

        /// <summary>
        /// Descobre, para cada parte alterada, a qual raiz ela pertence.
        ///
        /// A chave do dicionário é a ENTIDADE, não o EntityEntry: este último é um
        /// invólucro criado sob demanda e sem igualdade por valor —
        /// ChangeTracker.Entries() e Reference(nav).TargetEntry devolvem instâncias
        /// diferentes para a mesma entidade. Usando a entry como chave, a busca
        /// nunca casava e os itens ficavam silenciosamente fora do evento.
        ///
        /// Partes cuja raiz não está sendo salva são descartadas: sem raiz não há
        /// evento onde encaixá-las, e emiti-las soltas contrariaria o agrupamento.
        /// </summary>
        private static Dictionary<object, List<EntityEntry>> AgruparPartesPorRaiz(List<EntityEntry> entries)
        {
            var agrupamento = new Dictionary<object, List<EntityEntry>>(ReferenceEqualityComparer.Instance);

            foreach (var entry in entries.Where(e => e.Entity is IAuditavelComoParte))
            {
                var raiz = LocalizarRaiz(entry, entries);

                if (raiz is null)
                    continue;

                if (!agrupamento.TryGetValue(raiz.Entity, out var lista))
                    agrupamento[raiz.Entity] = lista = [];

                lista.Add(entry);
            }

            return agrupamento;
        }

        /// <summary>
        /// Encontra a raiz de uma parte, por dois caminhos complementares.
        ///
        /// A navegação inversa (item.Pedido) é a via direta, mas não é confiável:
        /// só está materializada se alguém a percorreu ou se o fixup do EF a
        /// preencheu. Ao criar um pedido com "Itens = [...]", os itens costumam
        /// ficar sem a referência de volta.
        ///
        /// O segundo caminho não depende disso: compara o valor da chave estrangeira
        /// com a chave primária das raízes sendo salvas. Funciona inclusive com
        /// entidades novas — pai e filho compartilham o mesmo valor temporário.
        /// </summary>
        private static EntityEntry? LocalizarRaiz(EntityEntry parte, List<EntityEntry> candidatas)
        {
            foreach (var fk in parte.Metadata.GetForeignKeys())
            {
                if (!typeof(IAuditavelRaiz).IsAssignableFrom(fk.PrincipalEntityType.ClrType))
                    continue;

                if (fk.DependentToPrincipal?.Name is { } navegacao)
                {
                    var alvo = parte.Reference(navegacao).TargetEntry;

                    if (alvo is not null)
                        return alvo;
                }

                var valoresFk = fk.Properties
                    .Select(p => parte.Property(p.Name).CurrentValue)
                    .ToArray();

                if (valoresFk.Any(v => v is null))
                    continue;

                foreach (var candidata in candidatas)
                {
                    if (candidata.Metadata.ClrType != fk.PrincipalEntityType.ClrType)
                        continue;

                    var valoresPk = fk.PrincipalKey.Properties
                        .Select(p => candidata.Property(p.Name).CurrentValue)
                        .ToArray();

                    if (valoresFk.SequenceEqual(valoresPk))
                        return candidata;
                }
            }

            return null;
        }

        /// <summary>
        /// Nome da navegação da raiz para a parte ("Itens"), obtido do metadata.
        /// Cai no nome do tipo apenas se o relacionamento não expuser navegação.
        /// </summary>
        private static string NomeDaColecao(EntityEntry parte)
        {
            foreach (var fk in parte.Metadata.GetForeignKeys())
            {
                if (!typeof(IAuditavelRaiz).IsAssignableFrom(fk.PrincipalEntityType.ClrType))
                    continue;

                if (fk.PrincipalToDependent?.Name is { } nome)
                    return nome;
            }

            return parte.Entity.GetType().Name;
        }

        // ── Montagem de snapshot e diff ───────────────────────────────────────

        /// <summary>
        /// Estado da entidade a partir do ChangeTracker.
        ///
        /// Só propriedades escalares mapeadas — navegações não aparecem aqui, o que
        /// é exatamente o desejado: serializar a entidade seguiria os
        /// relacionamentos, arrastaria a árvore inteira, entraria em loop nas
        /// referências circulares e dispararia lazy loading de dentro do interceptor.
        /// </summary>
        private static Dictionary<string, object?> MontarSnapshot(EntityEntry entry, TipoEventoAuditoria tipo)
        {
            var snapshot = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                if (DeveIgnorar(property))
                    continue;

                snapshot[property.Metadata.Name] = Normalizar(
                    tipo == TipoEventoAuditoria.Remocao
                        ? property.OriginalValue
                        : property.CurrentValue);
            }

            return snapshot;
        }

        private static List<AlteracaoCampo> MontarDiff(EntityEntry entry)
        {
            var alteracoes = new List<AlteracaoCampo>();

            foreach (var property in entry.Properties)
            {
                if (DeveIgnorar(property))
                    continue;

                // IsModified sozinho não basta: o EF pode marcar a propriedade como
                // modificada mesmo quando o valor final é igual ao original (por
                // exemplo, atribuição do mesmo valor). Comparar evita registrar
                // alterações que não aconteceram.
                if (!property.IsModified || Equals(property.OriginalValue, property.CurrentValue))
                    continue;

                alteracoes.Add(new AlteracaoCampo
                {
                    Campo = property.Metadata.Name,
                    De = Normalizar(property.OriginalValue),
                    Para = Normalizar(property.CurrentValue)
                });
            }

            return alteracoes;
        }

        /// <summary>
        /// Troca as chaves estrangeiras declaradas com [AuditarReferencia] por
        /// { id, descricao }, preservando quem era a entidade referenciada no
        /// momento do evento.
        ///
        /// Roda depois do commit de propósito: resolver a descrição pode exigir
        /// consulta, e consultar no meio de um SaveChanges em andamento seria
        /// reentrância no mesmo contexto. FindAsync verifica o ChangeTracker antes
        /// de ir ao banco, então quando a entidade já está carregada — o caso comum —
        /// não há consulta alguma.
        ///
        /// Se a referência não for encontrada, o id permanece sozinho no snapshot:
        /// auditoria incompleta é melhor que auditoria ausente.
        /// </summary>
        private static async Task DesnormalizarReferenciasAsync(
            EntityEntry entry,
            EventoAuditoria evento,
            CancellationToken cancellationToken)
        {
            var contexto = entry.Context;

            foreach (var property in entry.Properties)
            {
                var atributo = property.Metadata.PropertyInfo?
                    .GetCustomAttribute<AuditarReferenciaAttribute>();

                if (atributo is null)
                    continue;

                var valorId = property.CurrentValue;

                if (valorId is null)
                    continue;

                var referenciada = await contexto.FindAsync(
                    atributo.TipoReferenciado, [valorId], cancellationToken);

                if (referenciada is null)
                    continue;

                var descricao = atributo.TipoReferenciado
                    .GetProperty(atributo.PropriedadeDescricao)?
                    .GetValue(referenciada);

                evento.Referencias[property.Metadata.Name] = new ReferenciaAuditada
                {
                    Id = valorId.ToString() ?? string.Empty,
                    Descricao = descricao?.ToString()
                };
            }
        }

        private static object? Normalizar(object? valor) => valor switch
        {
            Enum enumerado => enumerado.ToString(),

            // decimal não é tipo nativo do BSON. Dentro de um object, o driver
            // grava { "_t": "System.Decimal", "_v": ... } — ilegível na trilha e
            // desconfortável de consultar. Texto invariante preserva a precisão
            // exata (150.00 continua "150.00", sem arredondar como double faria)
            // ao custo de não permitir comparação numérica direta no Mongo, o que
            // é aceitável para auditoria.
            decimal numero => numero.ToString(CultureInfo.InvariantCulture),

            _ => valor
        };

        /// <summary>
        /// Exclui propriedades marcadas com [NaoAuditar] — senhas, tokens e afins.
        /// Sem isto, o snapshot de um Usuario levaria o SenhaHash para o Mongo.
        /// </summary>
        private static bool DeveIgnorar(PropertyEntry property) =>
            property.Metadata.PropertyInfo?.GetCustomAttribute<NaoAuditarAttribute>() is not null;

        /// <summary>
        /// Chave primária como texto. Cobre chave composta unindo as partes, e
        /// texto (em vez de int) porque nem toda entidade usa id numérico.
        /// </summary>
        private static string ObterChave(EntityEntry entry)
        {
            var chaves = entry.Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => p.CurrentValue?.ToString() ?? string.Empty)
                .ToList();

            return chaves.Count > 0 ? string.Join("|", chaves) : string.Empty;
        }
    }
}
