import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';

import { AuthService } from '../../../core/services/auth.service';
import { PERFIL_ADMINISTRADOR } from '../../../core/models/auth.model';

/**
 * Moldura da área da aplicação: barra superior e menu lateral com os módulos.
 *
 * Separada do layout do AdminCenter porque as duas áreas atendem públicos
 * diferentes — aqui ficam as operações do dia a dia; lá, a configuração do
 * sistema e a trilha de auditoria.
 */
@Component({
  selector: 'app-layout-app',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatSidenavModule,
    MatToolbarModule,
  ],
  templateUrl: './layout-app.html',
  styleUrl: './layout-app.scss',
})
export class LayoutApp {
  private readonly auth = inject(AuthService);

  protected readonly sessao = this.auth.sessao;
  protected readonly menuAberto = signal(true);

  /** Atalho para o AdminCenter, exibido apenas a quem tem a role. */
  protected readonly ehAdministrador = this.auth.ehAdministrador;
  protected readonly perfilAdmin = PERFIL_ADMINISTRADOR;

  protected readonly modulos = [
    { rota: 'clientes', icone: 'groups', rotulo: 'Clientes' },
    { rota: 'pedidos', icone: 'receipt_long', rotulo: 'Pedidos' },
  ];

  protected alternarMenu(): void {
    this.menuAberto.update((aberto) => !aberto);
  }

  protected sair(): void {
    this.auth.sair();
  }
}
