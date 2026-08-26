import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AuthService } from '../../../core/services/auth.service';
import { ConexaoService } from '../../../core/services/conexao.service';
import { ConexaoDialog, TipoConexao } from '../conexao-dialog/conexao-dialog';
import {
  ConexaoMongoResponse,
  ConexaoPostgresResponse,
  StatusMigrationsResponse,
} from '../../../core/models/conexao.model';

@Component({
  selector: 'app-painel',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatToolbarModule,
  ],
  templateUrl: './painel.html',
  styleUrl: './painel.scss',
})
export class Painel {
  private readonly auth = inject(AuthService);
  private readonly conexoes = inject(ConexaoService);
  private readonly dialog = inject(MatDialog);
  private readonly aviso = inject(MatSnackBar);

  protected readonly sessao = this.auth.sessao;
  protected readonly carregando = signal(true);
  protected readonly aplicandoMigrations = signal(false);

  protected readonly postgres = signal<ConexaoPostgresResponse | null>(null);
  protected readonly mongo = signal<ConexaoMongoResponse | null>(null);
  protected readonly migrations = signal<StatusMigrationsResponse | null>(null);

  constructor() {
    this.carregar();
  }

  protected carregar(): void {
    this.carregando.set(true);

    // forkJoin dispara as três em paralelo — em série, a tela levaria o triplo
    // do tempo para montar sem nenhum ganho.
    //
    // O catchError por chamada é essencial: sem ele, o forkJoin inteiro falha
    // se UMA delas falhar, e uma conexão ainda não configurada (404) apagaria
    // da tela também as que já estão prontas.
    forkJoin({
      postgres: this.conexoes.obterPostgres().pipe(catchError(() => of(null))),
      mongo: this.conexoes.obterMongo().pipe(catchError(() => of(null))),
      migrations: this.conexoes.obterStatusMigrations().pipe(
        catchError((erro: HttpErrorResponse) =>
          of({
            configurado: false,
            conseguiuConectar: false,
            erro: erro.status === 404 ? 'Não configurado.' : 'Indisponível.',
            aplicadas: [],
            pendentes: [],
          } as StatusMigrationsResponse),
        ),
      ),
    }).subscribe((resultado) => {
      this.postgres.set(resultado.postgres);
      this.mongo.set(resultado.mongo);
      this.migrations.set(resultado.migrations);
      this.carregando.set(false);
    });
  }

  /**
   * Abre a edição da conexão.
   *
   * Recarrega ao fechar com alteração: o status das migrations depende da
   * conexão do Postgres, então trocar de banco muda o que os outros cartões
   * mostram — deixá-los com os dados antigos seria enganoso.
   */
  protected editar(tipo: TipoConexao): void {
    this.dialog
      .open(ConexaoDialog, {
        data: { tipo },
        autoFocus: false,
        width: '38rem',
        maxWidth: '94vw',
      })
      .afterClosed()
      .subscribe((alterou) => {
        if (alterou) {
          this.aviso.open('Conexão atualizada.', 'Fechar', { duration: 4000 });
          this.carregar();
        }
      });
  }

  protected aplicarMigrations(): void {
    this.aplicandoMigrations.set(true);

    this.conexoes.aplicarMigrations().subscribe({
      next: (resultado) => {
        this.aplicandoMigrations.set(false);
        this.aviso.open(resultado.mensagem, 'Fechar', { duration: 6000 });

        // A senha do super administrador só aparece uma vez. Se isso acontecer
        // aqui — porque o usuário foi removido do banco, por exemplo — avisamos
        // que o lugar de vê-la é o assistente, que trata a exibição com o
        // cuidado devido (destaque, cópia e confirmação de que foi anotada).
        if (resultado.superUsuario) {
          this.aviso.open(
            'Um super administrador foi criado. Abra o assistente de configuração para ver a senha.',
            'Fechar',
            { duration: 15000 },
          );
        }

        this.carregar();
      },
      error: (falha: HttpErrorResponse) => {
        this.aplicandoMigrations.set(false);
        this.aviso.open(
          falha.error?.mensagem ?? 'Não foi possível aplicar as migrations.',
          'Fechar',
          { duration: 8000 },
        );
      },
    });
  }

  protected sair(): void {
    this.auth.sair();
  }
}
