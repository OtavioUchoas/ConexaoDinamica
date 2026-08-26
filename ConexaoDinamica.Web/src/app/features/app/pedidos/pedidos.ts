import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { PedidoService } from '../../../core/services/pedido.service';
import { PedidoResponse } from '../../../core/models/conexao.model';
import { PedidoDialog } from './pedido-dialog/pedido-dialog';
import { ConfirmacaoDialog } from '../../../shared/confirmacao-dialog/confirmacao-dialog';

/**
 * Listagem de pedidos com busca, paginação e CRUD.
 *
 * Diferente de Clientes, aqui cada registro é um agregado: alterar o pedido e
 * seus itens gera UM evento de auditoria, com o diff apontando para dentro da
 * coleção.
 */
@Component({
  selector: 'app-pedidos',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './pedidos.html',
  styleUrl: './pedidos.scss',
})
export class Pedidos {
  private readonly servico = inject(PedidoService);
  private readonly dialog = inject(MatDialog);
  private readonly aviso = inject(MatSnackBar);

  protected readonly colunas = ['numero', 'cliente', 'status', 'total', 'dataCriacao', 'acoes'];

  protected readonly pedidos = signal<PedidoResponse[]>([]);
  protected readonly total = signal(0);
  protected readonly carregando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected busca = '';
  protected pagina = 1;
  protected tamanhoPagina = 10;

  private readonly digitacao = new Subject<string>();

  constructor() {
    this.digitacao
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.pagina = 1;
        this.carregar();
      });

    this.carregar();
  }

  protected aoDigitar(valor: string): void {
    this.busca = valor;
    this.digitacao.next(valor);
  }

  protected carregar(): void {
    this.carregando.set(true);
    this.erro.set(null);

    this.servico.listar(this.busca, this.pagina, this.tamanhoPagina).subscribe({
      next: (resultado) => {
        this.pedidos.set(resultado.itens);
        this.total.set(resultado.total);
        this.carregando.set(false);
      },
      error: (falha: HttpErrorResponse) => {
        this.carregando.set(false);
        this.pedidos.set([]);
        this.total.set(0);
        this.erro.set(
          falha.status === 503
            ? 'Banco de dados não configurado.'
            : 'Não foi possível carregar os pedidos.',
        );
      },
    });
  }

  protected mudarPagina(evento: PageEvent): void {
    this.pagina = evento.pageIndex + 1;
    this.tamanhoPagina = evento.pageSize;
    this.carregar();
  }

  protected novo(): void {
    this.abrirFormulario(null);
  }

  /**
   * Busca o pedido completo antes de abrir.
   *
   * Aqui isso é obrigatório, não apenas desejável: a listagem não traz os itens
   * (para não multiplicar o volume), então editar a partir da linha da grid
   * abriria o formulário sem nenhum item — e salvar assim os apagaria todos.
   *
   * De quebra, é essa chamada que registra a VISUALIZAÇÃO na trilha.
   */
  protected editar(pedido: PedidoResponse): void {
    this.servico.obterPorId(pedido.id).subscribe({
      next: (completo) => this.abrirFormulario(completo),
      error: () => this.aviso.open('Não foi possível abrir o pedido.', 'Fechar', { duration: 5000 }),
    });
  }

  private abrirFormulario(pedido: PedidoResponse | null): void {
    this.dialog
      .open(PedidoDialog, { data: { pedido }, autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((salvou) => {
        if (salvou) {
          this.aviso.open(pedido ? 'Pedido atualizado.' : 'Pedido cadastrado.', 'Fechar', {
            duration: 4000,
          });
          this.carregar();
        }
      });
  }

  protected remover(pedido: PedidoResponse): void {
    this.dialog
      .open(ConfirmacaoDialog, {
        data: {
          titulo: 'Remover pedido',
          mensagem: `Remover o pedido ${pedido.numero}? Os itens serão removidos junto, e a ação não pode ser desfeita.`,
          confirmar: 'Remover',
          perigo: true,
        },
      })
      .afterClosed()
      .subscribe((confirmou) => {
        if (!confirmou) {
          return;
        }

        this.servico.remover(pedido.id).subscribe({
          next: () => {
            this.aviso.open('Pedido removido.', 'Fechar', { duration: 4000 });

            if (this.pedidos().length === 1 && this.pagina > 1) {
              this.pagina--;
            }

            this.carregar();
          },
          error: (falha: HttpErrorResponse) => {
            this.aviso.open(
              falha.error?.message ?? 'Não foi possível remover o pedido.',
              'Fechar',
              { duration: 7000 },
            );
          },
        });
      });
  }

  /** Cor do chip por status, para dar leitura rápida na grid. */
  protected corDoStatus(status: string): string {
    return (
      {
        Rascunho: 'rascunho',
        Confirmado: 'confirmado',
        Enviado: 'enviado',
        Cancelado: 'cancelado',
      }[status] ?? 'rascunho'
    );
  }
}
