import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatStepperModule } from '@angular/material/stepper';

import { AuthService } from '../../core/services/auth.service';
import { ConexaoService } from '../../core/services/conexao.service';
import {
  ConexaoMongoRequest,
  ConexaoPostgresRequest,
  StatusMigrationsResponse,
  SuperUsuarioCriadoResponse,
  TesteConexaoResponse,
} from '../../core/models/conexao.model';
import { CredencialSuperUsuario } from '../../shared/credencial-super-usuario/credencial-super-usuario';

/**
 * Assistente de configuração inicial.
 *
 * É a primeira tela de uma instalação nova: enquanto as duas conexões não
 * existirem, o backend responde 503 a tudo que dependa de banco, e o
 * setupInterceptor traz o usuário para cá.
 *
 * A ordem dos passos não é arbitrária — cada um depende do anterior:
 *
 *   1. Acesso       o admin de bootstrap é o único login possível sem banco
 *   2. PostgreSQL   precisa existir antes de haver o que migrar
 *   3. MongoDB      a aplicação não opera sem trilha de auditoria
 *   4. Migrations   cria o schema e o super administrador
 *   5. Conclusão
 */
@Component({
  selector: 'app-setup',
  imports: [
    CredencialSuperUsuario,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatStepperModule,
  ],
  templateUrl: './setup.html',
  styleUrl: './setup.scss',
})
export class Setup {
  private readonly auth = inject(AuthService);
  private readonly conexoes = inject(ConexaoService);
  private readonly router = inject(Router);

  protected readonly ocupado = signal(false);

  // ── Passo 1: acesso ────────────────────────────────────────────────────
  protected loginAdmin = '';
  protected senhaAdmin = '';
  protected readonly erroLogin = signal<string | null>(null);
  protected readonly autenticado = this.auth.ehAdministrador;

  // ── Passo 2: PostgreSQL ────────────────────────────────────────────────
  protected postgres: ConexaoPostgresRequest = {
    host: 'localhost',
    porta: 5432,
    database: '',
    usuario: 'postgres',
    senha: '',
  };
  protected readonly testePostgres = signal<TesteConexaoResponse | null>(null);
  protected readonly postgresSalvo = signal(false);
  protected readonly erroPostgres = signal<string | null>(null);

  // ── Passo 3: MongoDB ───────────────────────────────────────────────────
  protected mongo: ConexaoMongoRequest = {
    host: 'localhost',
    porta: 27017,
    database: 'auditoria',
    usuario: '',
    senha: '',
    authSource: 'admin',
  };
  protected readonly testeMongo = signal<TesteConexaoResponse | null>(null);
  protected readonly mongoSalvo = signal(false);
  protected readonly erroMongo = signal<string | null>(null);

  // ── Passo 4: migrations ────────────────────────────────────────────────
  protected readonly statusMigrations = signal<StatusMigrationsResponse | null>(null);
  protected readonly migrationsAplicadas = signal(false);
  protected readonly superUsuario = signal<SuperUsuarioCriadoResponse | null>(null);
  protected readonly senhaAnotada = signal(false);
  protected readonly erroMigrations = signal<string | null>(null);

  /**
   * Só libera a conclusão depois que a senha do super administrador for
   * marcada como anotada. Ela aparece uma única vez — sair da tela sem copiar
   * significa perdê-la, e a única saída seria apagar o usuário e refazer o seed.
   */
  protected readonly podeConcluir = computed(
    () => this.migrationsAplicadas() && (this.superUsuario() === null || this.senhaAnotada()),
  );

  // ── Passo 1 ────────────────────────────────────────────────────────────

  protected entrar(): void {
    this.ocupado.set(true);
    this.erroLogin.set(null);

    this.auth
      .entrarComoAdmin({ login: this.loginAdmin, senha: this.senhaAdmin })
      .subscribe({
        next: () => this.ocupado.set(false),
        error: (falha: HttpErrorResponse) => {
          this.ocupado.set(false);
          this.erroLogin.set(
            falha.status === 401
              ? 'Credenciais inválidas.'
              : 'Não foi possível entrar. O servidor está em execução?',
          );
        },
      });
  }

  // ── Passo 2 ────────────────────────────────────────────────────────────

  protected testarPostgres(): void {
    this.ocupado.set(true);
    this.testePostgres.set(null);

    this.conexoes.testarPostgres(this.postgres).subscribe({
      next: (resultado) => {
        this.ocupado.set(false);
        // Chega com HTTP 200 mesmo quando falha: quem diz o resultado é o campo
        // sucesso, não o status. Por isso nada aqui vai para o bloco de erro.
        this.testePostgres.set(resultado);
      },
      error: () => {
        this.ocupado.set(false);
        this.erroPostgres.set('Falha ao contatar o servidor.');
      },
    });
  }

  protected salvarPostgres(): void {
    this.ocupado.set(true);
    this.erroPostgres.set(null);

    this.conexoes.salvarPostgres(this.postgres).subscribe({
      next: () => {
        this.ocupado.set(false);
        this.postgresSalvo.set(true);
      },
      error: (falha: HttpErrorResponse) => {
        this.ocupado.set(false);
        this.erroPostgres.set(this.descreverFalha(falha));
      },
    });
  }

  // ── Passo 3 ────────────────────────────────────────────────────────────

  protected testarMongo(): void {
    this.ocupado.set(true);
    this.testeMongo.set(null);

    this.conexoes.testarMongo(this.mongo).subscribe({
      next: (resultado) => {
        this.ocupado.set(false);
        this.testeMongo.set(resultado);
      },
      error: () => {
        this.ocupado.set(false);
        this.erroMongo.set('Falha ao contatar o servidor.');
      },
    });
  }

  protected salvarMongo(): void {
    this.ocupado.set(true);
    this.erroMongo.set(null);

    this.conexoes.salvarMongo(this.mongo).subscribe({
      next: () => {
        this.ocupado.set(false);
        this.mongoSalvo.set(true);
        this.carregarStatusMigrations();
      },
      error: (falha: HttpErrorResponse) => {
        this.ocupado.set(false);
        this.erroMongo.set(this.descreverFalha(falha));
      },
    });
  }

  // ── Passo 4 ────────────────────────────────────────────────────────────

  protected carregarStatusMigrations(): void {
    this.conexoes.obterStatusMigrations().subscribe({
      next: (status) => this.statusMigrations.set(status),
      error: () => this.statusMigrations.set(null),
    });
  }

  protected aplicarMigrations(): void {
    this.ocupado.set(true);
    this.erroMigrations.set(null);

    this.conexoes.aplicarMigrations().subscribe({
      next: (resultado) => {
        this.ocupado.set(false);

        if (!resultado.sucesso) {
          this.erroMigrations.set(resultado.mensagem);
          return;
        }

        this.migrationsAplicadas.set(true);
        this.superUsuario.set(resultado.superUsuario);
        this.carregarStatusMigrations();
      },
      error: (falha: HttpErrorResponse) => {
        this.ocupado.set(false);
        this.erroMigrations.set(falha.error?.mensagem ?? this.descreverFalha(falha));
      },
    });
  }

  protected concluir(): void {
    this.router.navigate(['/admin']);
  }

  private descreverFalha(falha: HttpErrorResponse): string {
    if (falha.status === 400 && falha.error?.errors) {
      return Object.values(falha.error.errors as Record<string, string[]>)
        .flat()
        .join(' ');
    }

    if (falha.status === 401 || falha.status === 403) {
      return 'Sessão sem permissão. Refaça o acesso administrativo.';
    }

    return falha.error?.message ?? 'Operação não concluída. Tente novamente.';
  }
}
