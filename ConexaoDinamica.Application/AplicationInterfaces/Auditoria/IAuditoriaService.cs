namespace ConexaoDinamica.Application.AplicationInterfaces.Auditoria
{
    /// <summary>
    /// Registro explícito de eventos de auditoria, para o que o interceptor do EF
    /// não tem como capturar.
    ///
    /// ── Por que visualização não pode ser automática ──────────────────────────
    /// O interceptor só é acionado por SaveChanges. Uma consulta não altera nada,
    /// não passa por ali, e portanto é invisível para ele — a chamada precisa
    /// partir de quem exibiu o dado.
    ///
    /// ── Quando chamar ────────────────────────────────────────────────────────
    /// Ao abrir o detalhe de um registro individual, não em listagens. A diferença
    /// importa por dois motivos:
    ///
    ///   Volume    — leituras superam escritas por ordens de grandeza, e uma
    ///               listagem paginada geraria centenas de eventos por tela.
    ///   Semântica — "acessou o cliente 42" é um fato auditável; "viu uma lista
    ///               onde 42 aparecia numa linha" é ruído: pode nem ter olhado.
    ///
    /// Para exportações e relatórios, registre o CRITÉRIO da consulta
    /// ("exportou clientes da cidade X, 340 registros"), nunca um evento por
    /// registro retornado.
    /// </summary>
    public interface IAuditoriaService
    {
        /// <summary>
        /// Registra que um usuário acessou o detalhe de um registro.
        /// </summary>
        /// <param name="tipoEntidade">Nome da entidade. Sem ele, um id solto é ambíguo: "42" de quê?</param>
        /// <param name="entidadeId">Identificador do registro acessado.</param>
        Task RegistrarVisualizacaoAsync(
            string tipoEntidade,
            string entidadeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registra que a trilha foi exportada para fora do sistema.
        ///
        /// É o evento mais importante desta interface. Exportar a auditoria é o
        /// gesto que tira os dados de dentro de qualquer controle de acesso: a
        /// partir dali o arquivo circula por e-mail, pen drive e pasta
        /// compartilhada. Sem este registro, a única ação que realmente esvazia a
        /// trilha seria a única que ela não veria.
        ///
        /// Segue o critério documentado acima: grava o FILTRO e o volume, nunca um
        /// evento por linha exportada.
        /// </summary>
        /// <param name="criterio">Filtros aplicados, em texto legível.</param>
        /// <param name="quantidade">Número de eventos que saíram no arquivo.</param>
        Task RegistrarExportacaoAsync(
            string criterio,
            int quantidade,
            CancellationToken cancellationToken = default);
    }
}
