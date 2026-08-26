import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface DadosConfirmacao {
  titulo: string;
  mensagem: string;
  confirmar?: string;
  cancelar?: string;
  /** Realça o botão de confirmação como ação destrutiva. */
  perigo?: boolean;
}

/**
 * Confirmação genérica para ações destrutivas.
 *
 * Fica em shared/ porque toda tela de CRUD precisa da mesma pergunta antes de
 * remover — e a alternativa, um confirm() do navegador, não segue o tema, não é
 * estilizável e bloqueia a thread da interface.
 */
@Component({
  selector: 'app-confirmacao-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>{{ dados.titulo }}</h2>
    <mat-dialog-content>
      <p>{{ dados.mensagem }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton (click)="fechar(false)">{{ dados.cancelar ?? 'Cancelar' }}</button>
      <button matButton="filled" [class.perigo]="dados.perigo" (click)="fechar(true)">
        {{ dados.confirmar ?? 'Confirmar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    p { margin: 0; max-width: 26rem; }
    .perigo {
      background: var(--mat-sys-error);
      color: var(--mat-sys-on-error);
    }
  `,
})
export class ConfirmacaoDialog {
  private readonly referencia = inject(MatDialogRef<ConfirmacaoDialog>);
  protected readonly dados: DadosConfirmacao = inject(MAT_DIALOG_DATA);

  protected fechar(confirmou: boolean): void {
    this.referencia.close(confirmou);
  }
}
