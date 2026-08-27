using ClosedXML.Excel;
using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Gera a planilha da trilha de auditoria com o ClosedXML.
    ///
    /// ── Por que duas abas ────────────────────────────────────────────────────
    /// O evento é aninhado (alterações, snapshot, partes) e a planilha é plana.
    /// Qualquer achatamento único perde alguma coisa:
    ///
    ///   uma linha por evento     -> não dá para filtrar nem pivotar por campo;
    ///                               as alterações viram texto numa célula
    ///   uma linha por alteração  -> o metadado se repete e visualizações ficam
    ///                               com metade das colunas vazias
    ///
    /// Duas abas ligadas pelo id do evento resolvem as duas leituras sem escolher
    /// entre elas: "Eventos" responde o que aconteceu, "Alterações" responde o que
    /// mudou campo a campo — e essa segunda é tabela dinâmica pronta.
    ///
    /// ── Por que XLSX e não CSV ───────────────────────────────────────────────
    /// Além das abas, o CSV entregaria tudo como texto e ainda esbarraria no
    /// Excel em português: separador ';' e BOM obrigatório, sob pena de o arquivo
    /// abrir numa coluna só ou com os acentos quebrados.
    /// </summary>
    public class ExportadorAuditoriaXlsx : IExportadorAuditoria
    {
        /// <summary>
        /// Formato aplicado às colunas de data. Deixar o Excel escolher exibiria
        /// só a data, escondendo a hora — e numa trilha a hora é metade do fato.
        /// </summary>
        private const string FormatoDataHora = "dd/MM/yyyy HH:mm:ss";

        public byte[] GerarPlanilha(IReadOnlyList<EventoAuditoria> eventos, string criterio)
        {
            using var planilha = new XLWorkbook();

            MontarAbaEventos(planilha, eventos, criterio);
            MontarAbaAlteracoes(planilha, eventos);

            using var memoria = new MemoryStream();
            planilha.SaveAs(memoria);

            return memoria.ToArray();
        }

        /// <summary>Uma linha por evento: o que aconteceu, quem fez, quando.</summary>
        private static void MontarAbaEventos(
            XLWorkbook planilha,
            IReadOnlyList<EventoAuditoria> eventos,
            string criterio)
        {
            var aba = planilha.Worksheets.Add("Eventos");

            // O critério fica dentro do arquivo, na primeira linha. Um XLSX solto
            // numa pasta não diz de que consulta veio, e sem isso ninguém sabe se
            // está olhando a trilha inteira ou um recorte.
            aba.Cell(1, 1).Value = "Filtro aplicado:";
            aba.Cell(1, 2).Value = criterio;
            aba.Cell(1, 1).Style.Font.Bold = true;

            aba.Cell(2, 1).Value = "Gerado em:";
            aba.Cell(2, 2).Value = DateTime.Now;
            aba.Cell(2, 2).Style.DateFormat.Format = FormatoDataHora;
            aba.Cell(2, 1).Style.Font.Bold = true;

            const int linhaCabecalho = 4;

            string[] colunas =
            [
                "Data/hora", "Evento", "Entidade", "Registro", "Usuário",
                "E-mail", "Origem (IP)", "Correlação", "Qtd. alterações",
                "Alterações", "Referências", "Id do evento"
            ];

            for (var i = 0; i < colunas.Length; i++)
                aba.Cell(linhaCabecalho, i + 1).Value = colunas[i];

            var linha = linhaCabecalho + 1;

            foreach (var evento in eventos)
            {
                aba.Cell(linha, 1).Value = evento.DataHora.ToLocalTime();
                aba.Cell(linha, 1).Style.DateFormat.Format = FormatoDataHora;

                aba.Cell(linha, 2).Value = evento.TipoEvento.ToString();
                aba.Cell(linha, 3).Value = evento.Entidade.Tipo;

                // Texto explícito: ids numéricos como "00123" perderiam os zeros à
                // esquerda, e o Excel alinharia à direita, sugerindo que são
                // quantidades.
                aba.Cell(linha, 4).SetValue(evento.Entidade.Id).Style.NumberFormat.Format = "@";

                aba.Cell(linha, 5).Value = evento.Usuario?.Nome ?? "sistema";
                aba.Cell(linha, 6).Value = evento.Usuario?.Email ?? string.Empty;
                aba.Cell(linha, 7).Value = evento.Origem?.Ip ?? string.Empty;
                aba.Cell(linha, 8).Value = evento.CorrelationId ?? string.Empty;
                aba.Cell(linha, 9).Value = evento.Alteracoes.Count;
                aba.Cell(linha, 10).Value = ResumirAlteracoes(evento);
                aba.Cell(linha, 11).Value = ResumirReferencias(evento);
                aba.Cell(linha, 12).Value = evento.Id.ToString();

                linha++;
            }

            Formatar(aba, linhaCabecalho, colunas.Length, linha - 1);
        }

        /// <summary>
        /// Uma linha por campo alterado — o formato que o Excel sabe agrupar.
        ///
        /// Só eventos com diff aparecem. Incluir visualização e adição com as
        /// colunas de campo vazias encheria a aba de linhas que não respondem à
        /// pergunta que ela existe para responder; quem quer o inventário completo
        /// tem a aba Eventos ao lado.
        /// </summary>
        private static void MontarAbaAlteracoes(XLWorkbook planilha, IReadOnlyList<EventoAuditoria> eventos)
        {
            var aba = planilha.Worksheets.Add("Alterações");

            const int linhaCabecalho = 1;

            string[] colunas =
            [
                "Data/hora", "Entidade", "Registro", "Usuário", "Campo",
                "De", "Para", "Id do evento"
            ];

            for (var i = 0; i < colunas.Length; i++)
                aba.Cell(linhaCabecalho, i + 1).Value = colunas[i];

            var linha = linhaCabecalho + 1;

            foreach (var evento in eventos)
            {
                foreach (var alteracao in evento.Alteracoes)
                {
                    aba.Cell(linha, 1).Value = evento.DataHora.ToLocalTime();
                    aba.Cell(linha, 1).Style.DateFormat.Format = FormatoDataHora;

                    aba.Cell(linha, 2).Value = evento.Entidade.Tipo;
                    aba.Cell(linha, 3).SetValue(evento.Entidade.Id).Style.NumberFormat.Format = "@";
                    aba.Cell(linha, 4).Value = evento.Usuario?.Nome ?? "sistema";
                    aba.Cell(linha, 5).Value = alteracao.Campo;

                    // De/Para saem como texto sempre. Os valores da trilha são
                    // heterogêneos por natureza — "10.07", "Confirmado",
                    // "removido" —, e deixar o Excel adivinhar transformaria
                    // alguns em número e outros em data, quebrando a comparação
                    // visual entre as duas colunas.
                    aba.Cell(linha, 6).SetValue(Texto(alteracao.De)).Style.NumberFormat.Format = "@";
                    aba.Cell(linha, 7).SetValue(Texto(alteracao.Para)).Style.NumberFormat.Format = "@";

                    aba.Cell(linha, 8).Value = evento.Id.ToString();

                    linha++;
                }
            }

            Formatar(aba, linhaCabecalho, colunas.Length, linha - 1);
        }

        /// <summary>
        /// Cabeçalho destacado, autofiltro, painel congelado e largura ajustada.
        ///
        /// Não é enfeite: sem congelar o cabeçalho, rolar mil linhas deixa quem lê
        /// sem saber que coluna está olhando, e é a primeira coisa que a pessoa
        /// faria à mão ao abrir o arquivo.
        /// </summary>
        private static void Formatar(IXLWorksheet aba, int linhaCabecalho, int colunas, int ultimaLinha)
        {
            var cabecalho = aba.Range(linhaCabecalho, 1, linhaCabecalho, colunas);
            cabecalho.Style.Font.Bold = true;
            cabecalho.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Com zero registros existe só o cabeçalho: aplicar autofiltro a um
            // intervalo de uma linha só faz o Excel reclamar do arquivo ao abrir.
            if (ultimaLinha > linhaCabecalho)
                aba.Range(linhaCabecalho, 1, ultimaLinha, colunas).SetAutoFilter();

            aba.SheetView.FreezeRows(linhaCabecalho);

            aba.Columns().AdjustToContents();

            // AdjustToContents segue a célula mais larga, e uma coluna de resumo
            // com um evento grande passaria da tela inteira.
            foreach (var coluna in aba.Columns())
            {
                if (coluna.Width > 60)
                    coluna.Width = 60;
            }
        }

        /// <summary>
        /// Condensa o diff numa célula: "Total: 10.07 → 10; Itens[15]: removido".
        /// </summary>
        private static string ResumirAlteracoes(EventoAuditoria evento) =>
            string.Join("; ", evento.Alteracoes.Select(a =>
                $"{a.Campo}: {Texto(a.De)} → {Texto(a.Para)}"));

        /// <summary>
        /// Usa a descrição desnormalizada, não o id. É o ponto inteiro de gravar a
        /// referência: "Cliente: dad" continua legível depois de o cliente sumir,
        /// "ClienteId: 7" não.
        /// </summary>
        private static string ResumirReferencias(EventoAuditoria evento) =>
            string.Join("; ", evento.Referencias.Select(r =>
                $"{r.Key}: {r.Value.Descricao ?? r.Value.Id}"));

        private static string Texto(object? valor) => valor?.ToString() ?? string.Empty;
    }
}
