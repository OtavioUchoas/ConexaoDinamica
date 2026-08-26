import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
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

import { ClienteService } from '../../../core/services/cliente.service';
import { ClienteResponse } from '../../../core/models/conexao.model';
import { ClienteDialog } from './cliente-dialog/cliente-dialog';
import { ConfirmacaoDialog } from '../../../shared/confirmacao-dialog/confirmacao-dialog';

/**
 * Listagem de clientes com busca, paginação e as operações de CRUD.
 *
 * Cada operação aqui gera evento na trilha de auditoria automaticamente, porque
 * Cliente é IAuditavelRaiz e o interceptor observa o SaveChanges. A exceção é a
 * visualização: ela só é registrada ao abrir o detalhe, o que acontece quando o
 * cliente é carregado para edição — a listagem não gera evento.
 */
@Component({
  selector: 'app-clientes',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
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
  templateUrl: './clientes.html',
  styleUrl: './clientes.scss',
})
export class Clientes {
  private readonly servico = inject(ClienteService);
  private readonly dialog = inject(MatDialog);
  private readonly aviso = inject(MatSnackBar);

  protected readonly colunas = ['nome', 'documento', 'email', 'dataCadastro', 'acoes'];

  protected readonly clientes = signal<ClienteResponse[]>([]);
  protected readonly total = signal(0);
  protected readonly carregando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected busca = '';
  protected pagina = 1;
  protected tamanhoPagina = 10;

  /**
   * Evita uma requisição por tecla digitada.
   *
   * Sem o debounce, "Padaria" dispararia sete buscas, e as respostas poderiam
   * chegar fora de ordem — a lista terminaria exibindo o resultado de "Pad" em
   * vez do termo completo.
   */
  private readonly digitacao = new Subject<string>();

  constructor() {
    this.digitacao
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        // Buscar volta para a primeira página: manter a página atual poderia
        // cair num intervalo que o novo termo nem alcança.
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
        this.clientes.set(resultado.itens);
        this.total.set(resultado.total);
        this.carregando.set(false);
      },
      error: (falha: HttpErrorResponse) => {
        this.carregando.set(false);
        this.clientes.set([]);
        this.total.set(0);
        this.erro.set(
          falha.status === 503
            ? 'Banco de dados não configurado.'
            : 'Não foi possível carregar os clientes.',
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
   * Abre a edição buscando o cliente pelo id, em vez de usar a linha da lista.
   *
   * São duas razões: o registro pode ter mudado desde que a lista foi carregada,
   * e é essa chamada ao detalhe que produz o evento de VISUALIZAÇÃO na trilha —
   * exatamente o comportamento pretendido, já que abrir para editar é acessar o
   * dado individual.
   */
  protected editar(cliente: ClienteResponse): void {
    this.servico.obterPorId(cliente.id).subscribe({
      next: (atual) => this.abrirFormulario(atual),
      error: () => this.aviso.open('Não foi possível abrir o cliente.', 'Fechar', { duration: 5000 }),
    });
  }

  private abrirFormulario(cliente: ClienteResponse | null): void {
    this.dialog
      .open(ClienteDialog, { data: { cliente }, autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((salvou) => {
        if (salvou) {
          this.aviso.open(
            cliente ? 'Cliente atualizado.' : 'Cliente cadastrado.',
            'Fechar',
            { duration: 4000 },
          );
          this.carregar();
        }
      });
  }

  protected remover(cliente: ClienteResponse): void {
    this.dialog
      .open(ConfirmacaoDialog, {
        data: {
          titulo: 'Remover cliente',
          mensagem: `Remover "${cliente.nome}"? Esta ação não pode ser desfeita.`,
          confirmar: 'Remover',
          perigo: true,
        },
      })
      .afterClosed()
      .subscribe((confirmou) => {
        if (!confirmou) {
          return;
        }

        this.servico.remover(cliente.id).subscribe({
          next: () => {
            this.aviso.open('Cliente removido.', 'Fechar', { duration: 4000 });

            // Remover o último item de uma página deixaria a lista vazia com
            // paginação apontando para além do fim.
            if (this.clientes().length === 1 && this.pagina > 1) {
              this.pagina--;
            }

            this.carregar();
          },
          error: (falha: HttpErrorResponse) => {
            // 409 é o cliente com pedidos: a FK usa Restrict para preservar o
            // histórico, e o backend devolve a explicação.
            this.aviso.open(
              falha.error?.message ?? 'Não foi possível remover o cliente.',
              'Fechar',
              { duration: 7000 },
            );
          },
        });
      });
  }
}
