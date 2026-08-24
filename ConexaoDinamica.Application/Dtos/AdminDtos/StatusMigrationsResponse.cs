namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Situação do schema no banco configurado: o que o AdminCenter precisa saber
    /// para decidir se deve oferecer o botão "Aplicar migrations".
    /// </summary>
    public class StatusMigrationsResponse
    {
        /// <summary>Existe configuração salva e completa.</summary>
        public bool Configurado { get; set; }

        /// <summary>
        /// A conexão foi aberta com sucesso. Separado de <see cref="Configurado"/>
        /// porque são falhas diferentes: configuração ausente é um estado inicial
        /// esperado; configuração presente que não conecta é erro de dados.
        /// </summary>
        public bool ConseguiuConectar { get; set; }

        /// <summary>Erro ao conectar, quando houver.</summary>
        public string? Erro { get; set; }

        /// <summary>Migrations já presentes no banco.</summary>
        public IReadOnlyList<string> Aplicadas { get; set; } = [];

        /// <summary>Migrations que existem no código mas ainda não no banco.</summary>
        public IReadOnlyList<string> Pendentes { get; set; } = [];
    }
}
