import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { AuthService } from '../../../core/services/auth.service';

/**
 * Acesso administrativo, separado do login de usuário.
 *
 * Antes os dois compartilhavam uma tela com um seletor. Separar tem uma razão
 * concreta além da organização: o seletor ANUNCIAVA, na tela pública, que
 * existe um acesso administrativo que funciona sem banco. Não era falha de
 * segurança — a credencial seguia protegida —, mas é informação que não precisa
 * ser oferecida a quem só quer entrar na aplicação.
 *
 * Não há link para cá a partir do login comum: quem opera o sistema conhece a
 * rota, e o assistente de configuração leva até ela quando necessário.
 */
@Component({
  selector: 'app-login-admin',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './login-admin.html',
  styleUrl: './login-admin.scss',
})
export class LoginAdmin {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected login = '';
  protected senha = '';

  protected readonly carregando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected entrar(): void {
    if (this.carregando()) {
      return;
    }

    this.carregando.set(true);
    this.erro.set(null);

    this.auth.entrarComoAdmin({ login: this.login, senha: this.senha }).subscribe({
      next: () => {
        this.carregando.set(false);
        this.router.navigate(['/admin/conexoes']);
      },
      error: (falha: HttpErrorResponse) => {
        this.carregando.set(false);
        this.erro.set(
          falha.status === 401
            ? 'Credenciais inválidas.'
            : falha.status === 0
              ? 'Não foi possível falar com o servidor.'
              : 'Não foi possível entrar. Tente novamente.',
        );
      },
    });
  }
}
