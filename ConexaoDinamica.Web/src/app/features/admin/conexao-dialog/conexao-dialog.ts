import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { ConexaoService } from '../../../core/services/conexao.service';
import {
  ConexaoMongoRequest,
  ConexaoPostgresRequest,
  TesteConexaoResponse,
} from '../../../core/models/conexao.model';

export type TipoConexao = 'postgres' | 'mongo';

/**
 * Edição de uma conexão já configurada.
 *
 * Existe porque o assistente de setup só aparece enquanto falta configuração:
 * depois de instalado, não haveria como trocar de servidor, corrigir uma senha
 * alterada ou apontar para outro ambiente.
 *
 * Um único diálogo atende os dois bancos. Os campos diferem (o Mongo tem
 * AuthSource, e aceita conexão anônima), mas o fluxo é idêntico — carregar,
 * testar, salvar —, e separar em dois componentes duplicaria essa parte.
 */
@Component({
  selector: 'app-conexao-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './conexao-dialog.html',
  styleUrl: './conexao-dialog.scss',
})
export class ConexaoDialog {
  private readonly conexoes = inject(ConexaoService);
  private readonly referencia = inject(MatDialogRef<ConexaoDialog>);

  protected readonly tipo: TipoConexao = inject(MAT_DIALOG_DATA).tipo;

  protected readonly ocupado = signal(false);
  protected readonly teste = signal<TesteConexaoResponse | null>(null);
  protected readonly erro = signal<string | null>(null);

  /**
   * Indica que já existe senha salva.
   *
   * O backend nunca devolve a senha — só informa se existe. Como o PUT grava
   * exatamente o que receber, salvar com o campo em branco APAGARIA a senha
   * atual. Por isso o formulário avisa que ela precisa ser redigitada.
   */
  protected readonly senhaJaDefinida = signal(false);

  /**
   * Confirmacao explicita para gravar sem senha.
   *
   * Salvar com o campo em branco apaga a senha atual e derruba a conexao — o
   * erro seguinte ("No password has been provided") aparece longe daqui e nao
   * aponta para a causa. Um aviso em texto nao basta: o campo abre vazio por
   * natureza, entao esquecer de redigitar e o caminho mais provavel, nao o
   * excepcional.
   *
   * Conectar sem senha continua possivel (autenticacao trust no Postgres, ou
   * Mongo local sem auth), mas passa a exigir intencao declarada.
   */
  protected readonly confirmaSemSenha = signal(false);

  /**
   * Verdadeiro quando o usuário está prestes a apagar a senha sem perceber.
   *
   * Método, e não computed: os campos do formulário são propriedades comuns
   * ligadas por ngModel, não signals. Um computed sem dependências reativas
   * calcularia uma única vez e devolveria esse valor para sempre — o aviso
   * nunca apareceria ao apagar o campo, nem sumiria ao redigitar.
   */
  protected removeriaSenha(): boolean {
    const senha = this.tipo === 'postgres' ? this.postgres.senha : this.mongo.senha;

    return this.senhaJaDefinida() && senha.trim() === '' && !this.confirmaSemSenha();
  }

  protected postgres: ConexaoPostgresRequest = {
    host: 'localhost',
    porta: 5432,
    database: '',
    usuario: 'postgres',
    senha: '',
  };

  protected mongo: ConexaoMongoRequest = {
    host: 'localhost',
    porta: 27017,
    database: 'auditoria',
    usuario: '',
    senha: '',
    authSource: 'admin',
  };

  constructor() {
    this.carregarAtual();
  }

  private carregarAtual(): void {
    this.ocupado.set(true);

    if (this.tipo === 'postgres') {
      this.conexoes.obterPostgres().subscribe({
        next: (atual) => {
          this.postgres = { ...atual, senha: '' };
          this.senhaJaDefinida.set(atual.senhaDefinida);
          this.ocupado.set(false);
        },
        // 404 significa "ainda não configurado" — não é erro, é o estado
        // inicial. O formulário simplesmente abre com os valores padrão.
        error: () => this.ocupado.set(false),
      });
      return;
    }

    this.conexoes.obterMongo().subscribe({
      next: (atual) => {
        this.mongo = { ...atual, senha: '' };
        this.senhaJaDefinida.set(atual.senhaDefinida);
        this.ocupado.set(false);
      },
      error: () => this.ocupado.set(false),
    });
  }

  protected testar(): void {
    this.ocupado.set(true);
    this.teste.set(null);
    this.erro.set(null);

    const requisicao =
      this.tipo === 'postgres'
        ? this.conexoes.testarPostgres(this.postgres)
        : this.conexoes.testarMongo(this.mongo);

    requisicao.subscribe({
      next: (resultado) => {
        this.ocupado.set(false);
        // Chega com HTTP 200 mesmo em falha: quem responde é o campo sucesso.
        this.teste.set(resultado);
      },
      error: () => {
        this.ocupado.set(false);
        this.erro.set('Falha ao contatar o servidor.');
      },
    });
  }

  protected salvar(): void {
    if (this.removeriaSenha()) {
      this.erro.set(
        'O campo de senha está vazio e existe uma senha salva. Redigite-a, ou ' +
          'confirme que a conexão deve passar a funcionar sem senha.',
      );
      return;
    }

    this.ocupado.set(true);
    this.erro.set(null);

    const requisicao =
      this.tipo === 'postgres'
        ? this.conexoes.salvarPostgres(this.postgres)
        : this.conexoes.salvarMongo(this.mongo);

    requisicao.subscribe({
      next: () => {
        this.ocupado.set(false);
        // true avisa o painel de que houve alteração, para ele recarregar.
        this.referencia.close(true);
      },
      error: (falha: HttpErrorResponse) => {
        this.ocupado.set(false);
        this.erro.set(this.descreverFalha(falha));
      },
    });
  }

  protected cancelar(): void {
    this.referencia.close(false);
  }

  private descreverFalha(falha: HttpErrorResponse): string {
    if (falha.status === 400 && falha.error?.errors) {
      return Object.values(falha.error.errors as Record<string, string[]>)
        .flat()
        .join(' ');
    }

    if (falha.status === 401 || falha.status === 403) {
      return 'Sessão sem permissão. Entre novamente como administrador.';
    }

    return falha.error?.message ?? 'Não foi possível salvar.';
  }
}
