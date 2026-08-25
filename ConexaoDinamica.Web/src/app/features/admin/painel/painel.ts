import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AuthService } from '../../../core/services/auth.service';
import { ConexaoService } from '../../../core/services/conexao.service';
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
    MatIconModule,
    MatProgressBarModule,
    MatToolbarModule,
  ],
  templateUrl: './painel.html',
  styleUrl: './painel.scss',
})
export class Painel {
  private readonly auth = inject(AuthService);
  private readonly conexoes = inject(ConexaoService);

  protected readonly sessao = this.auth.sessao;
  protected readonly carregando = signal(true);

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
      postgres: this.conexoes.obterPostgres().pipe(
        catchError(() => of(null)),
      ),
      mongo: this.conexoes.obterMongo().pipe(
        catchError(() => of(null)),
      ),
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

  protected sair(): void {
    this.auth.sair();
  }
}
