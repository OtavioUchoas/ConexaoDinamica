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
  ],
  templateUrl: './auditoria.html',
  styleUrl: './auditoria.scss',
})
export class Auditoria {
  private readonly servico = inject(AuditoriaService);

  protected readonly carregando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected readonly eventos = signal<EventoAuditoria[]>([]);
  protected readonly total = signal(0);
  protected readonly tiposEntidade = signal<string[]>([]);

  protected readonly tiposEvento: TipoEventoAuditoria[] = [
    'Adicao',
    'Alteracao',
    'Remocao',
    'Visualizacao',
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
