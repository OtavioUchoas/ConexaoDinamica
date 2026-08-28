import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

import { AuditoriaService } from '../../../core/services/auditoria.service';
import {
  EventoAuditoria,
  FiltroAuditoria,
  TipoEventoAuditoria,
} from '../../../core/models/conexao.model';

/**
 * Consulta da trilha de auditoria.
 *
 * Cada evento é uma linha expansível: fechada mostra quem, o quê e quando;
 * aberta mostra o diff, o snapshot, as referências e as partes do agregado.
 *
 * O detalhe fica recolhido de propósito. Um evento de alteração pode ter dezenas
 * de campos no snapshot, e mostrar tudo de uma vez transformaria a lista num
 * paredão — quem consulta auditoria costuma estar procurando UM evento, não
 * lendo todos.
 */
@Component({
  selector: 'app-auditoria',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
  ],
  templateUrl: './auditoria.html',
  styleUrl: './auditoria.scss',
})
export class Auditoria {
  private readonly servico = inject(AuditoriaService);

  protected readonly carregando = signal(false);
  protected readonly exportando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected readonly eventos = signal<EventoAuditoria[]>([]);
  protected readonly total = signal(0);
  protected readonly tiposEntidade = signal<string[]>([]);

  protected readonly tiposEvento: TipoEventoAuditoria[] = [
    'Adicao',
    'Alteracao',
    'Remocao',
    'Visualizacao',
    'Exportacao',
    'Autenticacao',
    'FalhaAutenticacao',
    'Configuracao',
  ];

  protected filtro: FiltroAuditoria = { pagina: 1, tamanhoPagina: 25 };

  protected readonly temFiltroAtivo = computed(() => this.eventos().length >= 0);

  constructor() {
    this.carregarTipos();
    this.consultar();
  }

  private carregarTipos(): void {
    this.servico.obterTiposEntidade().subscribe({
      next: (tipos) => this.tiposEntidade.set(tipos),
      // Falhar aqui só empobrece o filtro; não justifica interromper a tela.
      error: () => this.tiposEntidade.set([]),
    });
  }

  protected consultar(): void {
    this.carregando.set(true);
    this.erro.set(null);

    this.servico.consultar(this.filtro).subscribe({
      next: (resultado) => {
        this.eventos.set(resultado.itens);
        this.total.set(resultado.total);
        this.carregando.set(false);
      },
      error: (falha: HttpErrorResponse) => {
        this.carregando.set(false);
        this.eventos.set([]);
        this.total.set(0);

        // A leitura propaga falha de propósito: uma lista vazia por erro seria
        // indistinguível de "nenhum evento", e numa trilha de auditoria isso é
        // pior do que mostrar o problema.
        this.erro.set(
          falha.status === 503
            ? (falha.error?.message ?? 'Trilha indisponível.')
            : 'Não foi possível consultar a trilha.',
        );
      },
    });
  }

  /**
   * Baixa a planilha com o resultado do filtro atual.
   *
   * ── Por que não é um link simples ────────────────────────────────────────
   * O JWT viaja no cabeçalho Authorization, e um <a href> faz o NAVEGADOR
   * navegar — sem passar pelo HttpClient, portanto sem interceptor e sem token.
   * O servidor responderia 401. Buscar como blob mantém a requisição dentro do
   * Angular; o download é disparado depois, a partir do que já chegou.
   */
  protected exportar(): void {
    if (this.exportando()) {
      return;
    }

    this.exportando.set(true);
    this.erro.set(null);

    this.servico.exportar(this.filtro).subscribe({
      next: (arquivo) => {
        this.exportando.set(false);
        this.baixar(arquivo, `auditoria-${this.carimboDeTempo()}.xlsx`);
      },
      error: async (falha: HttpErrorResponse) => {
        this.exportando.set(false);

        // Com responseType 'blob', o corpo de erro TAMBÉM chega como blob — o
        // JSON com a mensagem viria como "[object Blob]" se lido direto.
        this.erro.set(await this.descreverFalhaDeBlob(falha));
      },
    });
  }

  /**
   * Entrega o arquivo ao navegador.
   *
   * O revokeObjectURL não é opcional: cada createObjectURL prende o blob na
   * memória da aba até a página ser recarregada, e exportações repetidas iriam
   * acumulando planilhas inteiras.
   */
  private baixar(arquivo: Blob, nome: string): void {
    const url = URL.createObjectURL(arquivo);
    const link = document.createElement('a');

    link.href = url;
    link.download = nome;
    link.click();

    URL.revokeObjectURL(url);
  }

  private async descreverFalhaDeBlob(falha: HttpErrorResponse): Promise<string> {
    if (falha.status === 503) {
      return 'Trilha indisponível: o MongoDB não está configurado.';
    }

    try {
      const corpo = JSON.parse(await (falha.error as Blob).text());
      return corpo.message ?? 'Não foi possível exportar a trilha.';
    } catch {
      return 'Não foi possível exportar a trilha.';
    }
  }

  /** Mesmo formato usado pelo servidor no nome do arquivo. */
  private carimboDeTempo(): string {
    const agora = new Date();
    const doisDigitos = (valor: number) => String(valor).padStart(2, '0');

    return (
      `${agora.getFullYear()}${doisDigitos(agora.getMonth() + 1)}${doisDigitos(agora.getDate())}` +
      `-${doisDigitos(agora.getHours())}${doisDigitos(agora.getMinutes())}${doisDigitos(agora.getSeconds())}`
    );
  }

  protected aplicarFiltro(): void {
    // Filtrar volta para a primeira página: manter a página atual poderia cair
    // num intervalo que o novo filtro nem alcança, mostrando lista vazia.
    this.filtro.pagina = 1;
    this.consultar();
  }

  protected limparFiltro(): void {
    this.filtro = { pagina: 1, tamanhoPagina: this.filtro.tamanhoPagina };
    this.consultar();
  }

  protected mudarPagina(evento: PageEvent): void {
    this.filtro.pagina = evento.pageIndex + 1;
    this.filtro.tamanhoPagina = evento.pageSize;
    this.consultar();
  }

  /** Filtra pelo registro exato do evento clicado. */
  protected filtrarPorRegistro(evento: EventoAuditoria): void {
    this.filtro.tipoEntidade = evento.entidade.tipo;
    this.filtro.entidadeId = evento.entidade.id;
    this.aplicarFiltro();
  }

  protected icone(tipo: TipoEventoAuditoria): string {
    return {
      Adicao: 'add_circle',
      Alteracao: 'edit',
      Remocao: 'delete',
      Visualizacao: 'visibility',
      Exportacao: 'download',
      Autenticacao: 'login',
      FalhaAutenticacao: 'gpp_bad',
      Configuracao: 'settings',
    }[tipo];
  }

  /** Converte o snapshot em pares para exibição, mantendo a ordem do documento. */
  protected paresDe(objeto: Record<string, unknown>): { chave: string; valor: string }[] {
    return Object.entries(objeto).map(([chave, valor]) => ({
      chave,
      valor: this.formatar(valor),
    }));
  }

  protected formatar(valor: unknown): string {
    if (valor === null || valor === undefined) {
      return '—';
    }

    // Objetos e listas aparecem em JSON compacto: são casos raros no snapshot
    // (que guarda escalares), então não vale uma renderização dedicada.
    return typeof valor === 'object' ? JSON.stringify(valor) : String(valor);
  }

  protected chavesDe(objeto: Record<string, unknown>): string[] {
    return Object.keys(objeto ?? {});
  }
}
