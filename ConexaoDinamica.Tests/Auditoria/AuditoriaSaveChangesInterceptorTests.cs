using System.Globalization;
using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Domain.Entidades.Clientes;
using ConexaoDinamica.Domain.Entidades.Pedidos;
using ConexaoDinamica.Domain.Entidades.Usuarios;
using ConexaoDinamica.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConexaoDinamica.Tests.Auditoria
{
    /// <summary>
    /// Testes do interceptor que transforma SaveChanges em eventos de auditoria.
    ///
    /// ── Por que esta classe existe ────────────────────────────────────────────
    /// O interceptor é a peça mais sutil do projeto: opera em duas fases porque
    /// nenhuma delas sozinha tem ao mesmo tempo o diff e a chave definitiva, e
    /// descobre o agregado raiz de uma parte comparando valores de chave
    /// estrangeira que ainda são temporários. Nada disso aparece na assinatura
    /// dos métodos, e um refactor bem-intencionado pode quebrar qualquer um
    /// desses pontos sem que nenhuma tela pare de funcionar — a auditoria apenas
    /// passaria a gravar errado, em silêncio.
    ///
    /// Por isso os testes verificam o CONTEÚDO do evento, e não se um evento foi
    /// emitido: a falha que interessa é o documento sutilmente errado.
    /// </summary>
    public class AuditoriaSaveChangesInterceptorTests
    {
        private static readonly DateTime Momento = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

        // ── Agregado simples ──────────────────────────────────────────────────

        [Fact]
        public async Task Adicao_resolve_a_chave_definitiva_no_evento_e_no_snapshot()
        {
            using var ambiente = new AmbienteAuditoria();

            await using var contexto = ambiente.NovoContexto();

            var cliente = NovoCliente();
            contexto.Clientes.Add(cliente);
            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            Assert.Equal(TipoEventoAuditoria.Adicao, evento.TipoEvento);
            Assert.Equal("Cliente", evento.Entidade.Tipo);
            Assert.True(cliente.Id > 0);
            Assert.Equal(cliente.Id.ToString(), evento.Entidade.Id);

            // O ponto central: na fase 1 o snapshot recebeu a chave temporária
            // negativa do EF. Sem a correção da fase 2, aqui estaria algo como
            // -2147482647 — um id que nunca existiu no banco.
            Assert.Equal(cliente.Id, Assert.IsType<int>(evento.Snapshot["Id"]));
            Assert.Equal("Acme", evento.Snapshot["Nome"]);

            // Adição guarda o estado inicial, não um diff.
            Assert.Empty(evento.Alteracoes);
        }

        [Fact]
        public async Task Evento_carrega_quem_fez_de_onde_e_sob_qual_correlacao()
        {
            using var ambiente = new AmbienteAuditoria();

            await using var contexto = ambiente.NovoContexto();
            contexto.Clientes.Add(NovoCliente());
            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            Assert.Equal("7", evento.Usuario?.Id);
            Assert.Equal("Fulano de Teste", evento.Usuario?.Nome);
            Assert.Equal("203.0.113.7", evento.Origem?.Ip);
            Assert.Equal("correlacao-de-teste", evento.CorrelationId);
        }

        [Fact]
        public async Task Snapshot_omite_as_propriedades_marcadas_com_NaoAuditar()
        {
            using var ambiente = new AmbienteAuditoria();

            await using var contexto = ambiente.NovoContexto();

            contexto.Usuarios.Add(new Usuario
            {
                Nome = "Ana",
                Email = "ana@teste.local",
                SenhaHash = "$2a$11$hash-que-nao-pode-vazar",
                Perfil = PerfilUsuario.Administrador,
                DataCriacao = Momento
            });

            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            // Sem o [NaoAuditar], a trilha viraria uma cópia paralela dos hashes
            // de senha do sistema, num banco com controle de acesso mais frouxo.
            Assert.False(evento.Snapshot.ContainsKey("SenhaHash"));
            Assert.DoesNotContain("hash-que-nao-pode-vazar", string.Join('|', evento.Snapshot.Values));

            // Enum vai como texto: reordenar o enum não pode reinterpretar
            // registros antigos da trilha.
            Assert.Equal("Administrador", evento.Snapshot["Perfil"]);
        }

        [Fact]
        public async Task Alteracao_registra_apenas_os_campos_cujo_valor_mudou()
        {
            using var ambiente = new AmbienteAuditoria();

            var id = await CriarClienteAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();
            var cliente = await contexto.Clientes.SingleAsync(c => c.Id == id);

            cliente.Email = "novo@acme.local";

            // Marcado como modificado mantendo o MESMO valor. É o caso que o
            // IsModified sozinho não distingue: o EF manda a coluna no UPDATE, mas
            // nada mudou e a trilha não pode afirmar que mudou.
            contexto.Entry(cliente).Property(c => c.Nome).IsModified = true;

            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            Assert.Equal(TipoEventoAuditoria.Alteracao, evento.TipoEvento);

            var alteracao = Assert.Single(evento.Alteracoes);
            Assert.Equal("Email", alteracao.Campo);
            Assert.Equal("contato@acme.local", alteracao.De);
            Assert.Equal("novo@acme.local", alteracao.Para);

            // O diff é para o humano; o snapshot é a âncora do estado final.
            Assert.Equal("novo@acme.local", evento.Snapshot["Email"]);
        }

        [Fact]
        public async Task Remocao_guarda_o_ultimo_estado_antes_de_sumir()
        {
            using var ambiente = new AmbienteAuditoria();

            var id = await CriarClienteAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();
            contexto.Clientes.Remove(await contexto.Clientes.SingleAsync(c => c.Id == id));
            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            Assert.Equal(TipoEventoAuditoria.Remocao, evento.TipoEvento);
            Assert.Equal(id.ToString(), evento.Entidade.Id);

            // Sem isto o registro apagado não sobreviveria em lugar nenhum: o
            // snapshot é a única cópia do que existia.
            Assert.Equal("Acme", evento.Snapshot["Nome"]);
            Assert.Equal("12345678000199", evento.Snapshot["Documento"]);
        }

        // ── Agregado com partes ───────────────────────────────────────────────

        [Fact]
        public async Task Pedido_novo_com_itens_gera_um_unico_evento_com_as_partes_resolvidas()
        {
            using var ambiente = new AmbienteAuditoria();

            var clienteId = await CriarClienteAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();

            var pedido = NovoPedido(clienteId);
            contexto.Pedidos.Add(pedido);
            await contexto.SaveChangesAsync();

            // Raiz e partes foram criadas na mesma operação: neste momento pai e
            // filho compartilhavam apenas a chave temporária negativa, e é por ela
            // que o interceptor precisou associá-los.
            var evento = ambiente.Repositorio.Unico;

            Assert.Equal("Pedido", evento.Entidade.Tipo);
            Assert.Equal(pedido.Id.ToString(), evento.Entidade.Id);

            var itens = evento.Partes["Itens"];
            Assert.Equal(2, itens.Count);

            foreach (var item in itens)
            {
                Assert.True(Assert.IsType<int>(item["Id"]) > 0);
                Assert.Equal(pedido.Id, Assert.IsType<int>(item["PedidoId"]));
            }

            // Entrada de parte é mudança do agregado e aparece no diff da raiz.
            Assert.Equal(2, evento.Alteracoes.Count);
            Assert.All(evento.Alteracoes, a => Assert.Equal("adicionado", a.Para));
        }

        [Fact]
        public async Task Referencia_declarada_e_gravada_com_a_descricao_do_momento()
        {
            using var ambiente = new AmbienteAuditoria();

            var clienteId = await CriarClienteAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();
            contexto.Pedidos.Add(NovoPedido(clienteId));
            await contexto.SaveChangesAsync();

            var referencia = ambiente.Repositorio.Unico.Referencias["ClienteId"];

            Assert.Equal(clienteId.ToString(), referencia.Id);

            // O id sozinho é rastreável mas não informativo: daqui a um ano o
            // cliente pode ter sido renomeado, e a trilha precisa dizer quem ele
            // era no momento do fato.
            Assert.Equal("Acme", referencia.Descricao);
        }

        [Fact]
        public async Task Snapshot_grava_decimal_como_texto_de_precisao_exata()
        {
            using var ambiente = new AmbienteAuditoria();

            var clienteId = await CriarClienteAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();
            contexto.Pedidos.Add(NovoPedido(clienteId));
            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            // Texto invariante: decimal não é tipo nativo do BSON, e dentro de um
            // object o driver gravaria { _t, _v }. Os centavos precisam sobreviver
            // literalmente — "150.00", não 150.
            Assert.Equal("150.00", evento.Snapshot["Total"]);
            Assert.Equal("Rascunho", evento.Snapshot["Status"]);
        }

        [Fact]
        public async Task Alteracao_de_item_entra_no_diff_do_pedido_com_caminho_qualificado()
        {
            using var ambiente = new AmbienteAuditoria();

            var (pedidoId, _) = await CriarPedidoAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();

            var pedido = await contexto.Pedidos
                .Include(p => p.Itens)
                .SingleAsync(p => p.Id == pedidoId);

            var item = pedido.Itens.OrderBy(i => i.Descricao).First();
            item.Quantidade = 5;
            pedido.Total = 250.00m;

            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            Assert.Equal("Pedido", evento.Entidade.Tipo);

            var alteracaoDoItem = Assert.Single(
                evento.Alteracoes, a => a.Campo == $"Itens[{item.Id}].Quantidade");

            Assert.Equal(2, alteracaoDoItem.De);
            Assert.Equal(5, alteracaoDoItem.Para);

            // A raiz e a parte convivem no mesmo diff — é isso que permite ler "o
            // que essa operação fez" sem abrir outro evento.
            Assert.Contains(evento.Alteracoes, a => a.Campo == "Total");
        }

        [Fact]
        public async Task Item_removido_vai_para_PartesRemovidas_com_o_conteudo_preservado()
        {
            using var ambiente = new AmbienteAuditoria();

            var (pedidoId, _) = await CriarPedidoAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();

            var pedido = await contexto.Pedidos
                .Include(p => p.Itens)
                .SingleAsync(p => p.Id == pedidoId);

            var removido = pedido.Itens.OrderBy(i => i.Descricao).First();
            contexto.Remove(removido);
            pedido.Total = 50.00m;

            await contexto.SaveChangesAsync();

            var evento = ambiente.Repositorio.Unico;

            // O diff diz apenas "existente -> removido". Sem PartesRemovidas, a
            // descrição e o preço do item apagado não sobreviveriam em lugar
            // nenhum do evento.
            var perdido = Assert.Single(evento.PartesRemovidas["Itens"]);
            Assert.Equal("Item A", perdido["Descricao"]);

            // Compara o VALOR, não o texto: aqui o preço voltou do banco, e a
            // quantidade de casas depende do provider (o SQLite dos testes guarda
            // decimal como texto e devolve "50.0"; o Postgres, numeric(18,2),
            // devolve "50.00"). O formato exato de Normalizar é verificado em
            // Snapshot_grava_decimal_como_texto_de_precisao_exata, onde o valor
            // nunca passa pelo banco.
            Assert.Equal(50.00m, decimal.Parse(
                (string)perdido["PrecoUnitario"]!, CultureInfo.InvariantCulture));

            var alteracao = Assert.Single(
                evento.Alteracoes, a => a.Campo == $"Itens[{removido.Id}]");
            Assert.Equal("removido", alteracao.Para);

            // Partes descreve o que É; a removida não pode aparecer ali.
            Assert.False(evento.Partes.ContainsKey("Itens"));
        }

        [Fact]
        public async Task Parte_sem_a_raiz_na_mesma_operacao_nao_gera_evento()
        {
            using var ambiente = new AmbienteAuditoria();

            await CriarPedidoAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();

            var item = await contexto.ItensPedido.OrderBy(i => i.Descricao).FirstAsync();
            item.Quantidade = 9;

            await contexto.SaveChangesAsync();

            // Comportamento deliberado: uma parte só é auditada dentro do evento
            // da sua raiz, e aqui a raiz não está sendo salva — não há evento onde
            // encaixá-la. Na prática o fluxo de edição de pedido sempre recalcula
            // o Total, então a raiz entra junto; este teste fixa a consequência
            // para quem um dia gravar um item por fora.
            Assert.Empty(ambiente.Repositorio.Eventos);
        }

        // ── A auditoria não derruba a operação de negócio ─────────────────────

        [Fact]
        public async Task Falha_ao_publicar_nao_chega_ao_chamador_do_SaveChanges()
        {
            using var ambiente = new AmbienteAuditoria();
            ambiente.Repositorio.FalharAoRegistrar = true;

            await using var contexto = ambiente.NovoContexto();
            contexto.Clientes.Add(NovoCliente());

            var linhas = await contexto.SaveChangesAsync();

            // A publicação acontece depois do commit: uma exceção ali reportaria
            // como falha uma operação que deu certo.
            Assert.Equal(1, linhas);
            Assert.Equal(1, await contexto.Clientes.CountAsync());
        }

        [Fact]
        public async Task Falha_ao_coletar_nao_chega_ao_chamador_do_SaveChanges()
        {
            using var ambiente = new AmbienteAuditoria();
            ambiente.Contexto.FalharAoObterUsuario = true;

            await using var contexto = ambiente.NovoContexto();
            contexto.Clientes.Add(NovoCliente());

            var linhas = await contexto.SaveChangesAsync();

            Assert.Equal(1, linhas);

            // Perder a trilha desta operação é o preço, e ele é pago no log em
            // nível Error — não impedindo o usuário de trabalhar.
            Assert.Empty(ambiente.Repositorio.Eventos);
        }

        // ── Apoio ─────────────────────────────────────────────────────────────

        private static Cliente NovoCliente() => new()
        {
            Nome = "Acme",
            Documento = "12345678000199",
            Email = "contato@acme.local",
            DataCadastro = Momento
        };

        private static Pedido NovoPedido(int clienteId) => new()
        {
            Numero = "PED-0001",
            ClienteId = clienteId,
            Status = StatusPedido.Rascunho,
            Total = 150.00m,
            DataCriacao = Momento,
            Itens =
            [
                new ItemPedido { Descricao = "Item A", Quantidade = 2, PrecoUnitario = 50.00m },
                new ItemPedido { Descricao = "Item B", Quantidade = 1, PrecoUnitario = 50.00m }
            ]
        };

        /// <summary>
        /// Cria o cliente e descarta os eventos: quem chama está montando o cenário,
        /// não observando esta gravação.
        /// </summary>
        private static async Task<int> CriarClienteAsync(AmbienteAuditoria ambiente)
        {
            await using var contexto = ambiente.NovoContexto();

            var cliente = NovoCliente();
            contexto.Clientes.Add(cliente);
            await contexto.SaveChangesAsync();

            ambiente.Repositorio.Eventos.Clear();

            return cliente.Id;
        }

        private static async Task<(int PedidoId, int ClienteId)> CriarPedidoAsync(AmbienteAuditoria ambiente)
        {
            var clienteId = await CriarClienteAsync(ambiente);

            await using var contexto = ambiente.NovoContexto();

            var pedido = NovoPedido(clienteId);
            contexto.Pedidos.Add(pedido);
            await contexto.SaveChangesAsync();

            ambiente.Repositorio.Eventos.Clear();

            return (pedido.Id, clienteId);
        }
    }
}
