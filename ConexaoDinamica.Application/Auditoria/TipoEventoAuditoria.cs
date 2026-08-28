namespace ConexaoDinamica.Application.Auditoria
{
    public enum TipoEventoAuditoria
    {
        Adicao = 1,
        Alteracao = 2,
        Remocao = 3,
        Visualizacao = 4,

        /// <summary>
        /// Saída de dados da trilha para fora do sistema.
        ///
        /// Não é uma visualização a mais: quem exporta leva consigo o histórico
        /// completo dos registros que casaram com o filtro, e esse arquivo passa a
        /// existir fora de qualquer controle de acesso. O evento registra o
        /// CRITÉRIO e o volume, não os registros — um evento por linha exportada
        /// inflaria a trilha sem acrescentar nada.
        /// </summary>
        Exportacao = 5,

        /// <summary>
        /// Autenticação bem-sucedida.
        ///
        /// O interceptor não tem como capturar: entrar no sistema não altera dado
        /// nenhum. E sem este evento a trilha sabe o que cada um fez, mas não sabe
        /// dizer desde quando estava lá dentro.
        /// </summary>
        Autenticacao = 6,

        /// <summary>
        /// Tentativa de autenticação recusada.
        ///
        /// Separado do sucesso, e não distinguido por uma flag, porque a pergunta
        /// que ele responde é outra e é feita sozinha: "quantas tentativas
        /// falharam para esta conta esta noite?". Com uma flag dentro do evento,
        /// isso exigiria varrer todas as autenticações; como tipo próprio, é um
        /// filtro.
        ///
        /// É o único evento da trilha cujo Usuario vem vazio por definição — quem
        /// não autenticou não tem identidade. O que sobra é a Origem (IP, agente) e
        /// o identificador informado, que é justamente o material de uma
        /// investigação de tentativa de invasão.
        /// </summary>
        FalhaAutenticacao = 7
    }
}
