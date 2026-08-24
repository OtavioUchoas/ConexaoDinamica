namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Configuração atual do MongoDB. Como no Postgres, não existe campo de senha:
    /// apenas <see cref="SenhaDefinida"/>, indicando se há uma salva.
    /// </summary>
    public class ConexaoMongoResponse
    {
        public string Host { get; set; } = string.Empty;

        public int Porta { get; set; }

        public string Database { get; set; } = string.Empty;

        public string Usuario { get; set; } = string.Empty;

        public string AuthSource { get; set; } = string.Empty;

        /// <summary>Existe senha salva? O valor em si nunca sai do servidor.</summary>
        public bool SenhaDefinida { get; set; }

        public bool EstaCompleta { get; set; }

        public DateTime DataAtualizacao { get; set; }
    }
}
