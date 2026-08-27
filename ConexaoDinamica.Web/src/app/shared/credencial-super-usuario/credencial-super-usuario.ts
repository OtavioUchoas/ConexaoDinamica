import { Component, computed, input, model, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';

import { SuperUsuarioCriadoResponse } from '../../core/models/conexao.model';

/**
 * Cartão com a senha do super administrador recém-criado.
 *
 * ── Por que fica em shared/ ──────────────────────────────────────────────────
 * O seed roda dentro de "aplicar migrations", e esse botão existe em DOIS
 * lugares: no passo 4 do assistente e no painel do AdminCenter. A senha é
 * gerada aleatoriamente e devolvida uma única vez na resposta — o banco guarda
 * apenas o hash BCrypt.
 *
 * Enquanto só o assistente sabia exibi-la, aplicar as migrations pelo painel
 * significava perdê-la: o painel recebia a senha na resposta e mandava o
 * usuário abrir o assistente, que nada mostraria, porque o seed é idempotente
 * e não recria o usuário existente. A senha existia por um instante e era
 * descartada, sem nenhuma forma de recuperá-la.
 *
 * ── Sobre exibir a senha em texto puro ───────────────────────────────────────
 * Não é uma exposição nova: ela já trafega em texto na resposta do endpoint, e
 * quem está nesta tela já se autenticou como administrador. Esconder na
 * interface esconderia apenas de quem tem o direito de vê-la.
 */
@Component({
  selector: 'app-credencial-super-usuario',
  imports: [MatButtonModule, MatCardModule, MatCheckboxModule, MatIconModule],
  template: `
    <mat-card class="credencial">
      <mat-card-header>
        <mat-icon mat-card-avatar>key</mat-icon>
        <mat-card-title>Super administrador criado</mat-card-title>
        <mat-card-subtitle>{{ dados().aviso }}</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <!--
          A distinção importa: este é um usuário da TABELA de usuários, usado
          para entrar na aplicação. Não se confunde com o admin de bootstrap,
          que vive na configuração e serve só para acessar o AdminCenter.
        -->
        <p class="explicacao">
          Use estas credenciais para entrar na <strong>aplicação</strong>. O acesso ao
          AdminCenter continua sendo o do administrador de bootstrap, definido na
          configuração do servidor.
        </p>

        <p class="rotulo">E-mail</p>
        <p class="valor">{{ dados().email }}</p>

        <p class="rotulo">Senha provisória</p>
        <p class="valor senha">{{ dados().senhaProvisoria }}</p>

        <button matButton (click)="copiar()">
          <mat-icon>{{ copiada() ? 'check' : 'content_copy' }}</mat-icon>
          {{ copiada() ? 'Copiada' : 'Copiar senha' }}
        </button>

        <mat-checkbox [checked]="anotada()" (change)="anotada.set($event.checked)">
          {{ rotuloConfirmacao() }}
        </mat-checkbox>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .credencial {
      margin: 1rem 0;
      border: 2px solid var(--mat-sys-primary);
    }

    .explicacao {
      margin: 0.75rem 0 0;
      font-size: 0.8rem;
      line-height: 1.45;
      color: var(--mat-sys-on-surface-variant);
    }

    .rotulo {
      margin: 0.75rem 0 0.15rem;
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--mat-sys-on-surface-variant);
    }

    .valor {
      margin: 0;
      font-family: ui-monospace, monospace;
      font-size: 0.95rem;
      word-break: break-all;
    }

    .senha {
      font-size: 1.15rem;
      font-weight: 600;
      padding: 0.5rem 0.75rem;
      border-radius: 0.375rem;
      background: var(--mat-sys-surface-container-highest);
      /* Um clique seleciona a senha inteira, para quem copia na mão. */
      user-select: all;
    }

    button {
      margin: 0.75rem 0.75rem 0.5rem 0;
    }
  `,
})
export class CredencialSuperUsuario {
  /** A senha e o e-mail devolvidos pelo endpoint de migrations. */
  readonly dados = input.required<SuperUsuarioCriadoResponse>();

  /**
   * Confirmação de que a senha foi guardada.
   *
   * É um `model` porque os dois usos reagem de formas diferentes: o assistente
   * o consulta para liberar a conclusão do passo, e o painel o usa para
   * dispensar o cartão. Exigir o gesto explícito em vez de um "X" evita fechar
   * sem querer aquilo que não pode ser reaberto.
   */
  readonly anotada = model(false);

  /** Deixa claro, no painel, que o cartão some ao marcar. */
  readonly dispensavel = input(false);

  protected readonly copiada = signal(false);

  protected readonly rotuloConfirmacao = computed(() =>
    this.dispensavel()
      ? 'Anotei a senha em local seguro (isto fecha o aviso)'
      : 'Anotei a senha em local seguro',
  );

  protected async copiar(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.dados().senhaProvisoria);
      this.copiada.set(true);
    } catch {
      // A Clipboard API exige contexto seguro (HTTPS ou localhost) e permissão.
      // Falhar não é problema: a senha está visível na tela para cópia manual.
      this.copiada.set(false);
    }
  }
}
