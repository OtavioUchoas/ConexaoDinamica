import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';

import { AuthService } from '../../../core/services/auth.service';

/**
 * Moldura do AdminCenter: barra superior, navegação e área de conteúdo.
 *
 * Existe para que cada tela administrativa cuide apenas do próprio conteúdo.
 * Antes, cada componente desenhava a própria toolbar — duplicação que só
 * cresceria a cada tela nova, com o risco de divergirem entre si.
 */
@Component({
  selector: 'app-layout-admin',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatButtonModule,
    MatIconModule,
    MatToolbarModule,
  ],
  templateUrl: './layout-admin.html',
  styleUrl: './layout-admin.scss',
})
export class LayoutAdmin {
  private readonly auth = inject(AuthService);

  protected readonly sessao = this.auth.sessao;

  protected readonly itens = [
    { rota: 'conexoes', icone: 'storage', rotulo: 'Conexões' },
    { rota: 'auditoria', icone: 'history', rotulo: 'Auditoria' },
  ];

  protected sair(): void {
    this.auth.sair();
  }
}
