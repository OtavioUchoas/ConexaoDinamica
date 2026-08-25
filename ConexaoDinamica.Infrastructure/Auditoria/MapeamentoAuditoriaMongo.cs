using ConexaoDinamica.Application.Auditoria;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Ensina o driver do Mongo a serializar o EventoAuditoria.
    ///
    /// ── Por que existe ────────────────────────────────────────────────────────
    /// A alternativa seria anotar as propriedades com atributos do driver
    /// ([BsonId], [BsonRepresentation], [BsonNoId]), mas isso levaria
    /// MongoDB.Bson para dentro da Application — justamente o acoplamento que o
    /// desenho evita. O mapeamento fica aqui, na única camada que conhece o driver.
    ///
    /// Três ajustes são necessários, todos descobertos inspecionando o documento
    /// realmente gravado:
    ///
    /// 1. Guid: a partir da versão 3 o driver não assume representação padrão e
    ///    lança "GuidSerializer cannot serialize a Guid when GuidRepresentation is
    ///    Unspecified". Standard é a recomendada (UUID binário subtipo 4).
    ///
    /// 2. Id em subdocumentos: a convenção do driver transforma qualquer
    ///    propriedade chamada "Id" em "_id", inclusive dentro de objetos
    ///    aninhados. O resultado era { Entidade: { Tipo, _id } } — confuso de
    ///    consultar e sugerindo uma chave que não existe ali.
    ///
    /// 3. Enums: gravados como número por padrão, o que torna a trilha ilegível
    ///    ("TipoEvento: 1") e frágil — reordenar o enum reinterpretaria os
    ///    registros antigos. Mesma escolha feita para PerfilUsuario no Postgres.
    /// </summary>
    internal static class MapeamentoAuditoriaMongo
    {
        private static readonly object Lock = new();
        private static bool _registrado;

        /// <summary>
        /// Idempotente: BsonClassMap.RegisterClassMap lança se a mesma classe for
        /// registrada duas vezes, e AddInfrastructure pode ser chamado mais de uma
        /// vez em cenários de teste.
        /// </summary>
        public static void Registrar()
        {
            if (_registrado)
                return;

            lock (Lock)
            {
                if (_registrado)
                    return;

                // Convenções precisam ser registradas ANTES dos class maps:
                // o AutoMap já as aplica ao construir o mapeamento.
                var convencoes = new ConventionPack
                {
                    new EnumRepresentationConvention(BsonType.String)
                };

                ConventionRegistry.Register(
                    "AuditoriaConvencoes",
                    convencoes,
                    tipo => tipo.Namespace == typeof(EventoAuditoria).Namespace);

                RegistrarSeNecessario<EventoAuditoria>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(e => e.Id)
                      .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                });

                // Sem SetIdMember(null), o "Id" destes subdocumentos vira "_id".
                RegistrarSeNecessario<EntidadeAuditada>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIdMember(null);
                });

                RegistrarSeNecessario<UsuarioAuditado>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIdMember(null);
                });

                RegistrarSeNecessario<ReferenciaAuditada>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIdMember(null);
                });

                _registrado = true;
            }
        }

        private static void RegistrarSeNecessario<T>(Action<BsonClassMap<T>> configurar)
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
                BsonClassMap.RegisterClassMap(configurar);
        }
    }
}
