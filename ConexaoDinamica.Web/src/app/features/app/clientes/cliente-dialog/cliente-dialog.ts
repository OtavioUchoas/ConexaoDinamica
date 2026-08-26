import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { ClienteService } from '../../../../core/services/cliente.service';
import { ClienteRequest, ClienteResponse } from '../../../../core/models/conexao.model';

/**
 * Cadastro e edição de cliente no mesmo diálogo.
 *
 * Os dois formulários seriam idênticos — mesmos campos, mesmas regras —, e
 * mantê-los separados garantiria que divergissem com o tempo: um campo novo
 * entraria só num deles. A diferença fica em duas linhas: o título e qual
 * método do serviço é chamado ao salvar.
 */
@Component({
  selector: 'app-cliente-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  templateUrl: './cliente-dialog.html',
  styleUrl: './cliente-dialog.scss',
})
export class ClienteDialog {
  private readonly servico = inject(ClienteService);
  private readonly referencia = inject(MatDialogRef<ClienteDialog>);

  /** Cliente recebido para edição, ou null para cadastro. */
  private readonly existente: ClienteResponse | null = inject(MAT_DIALOG_DATA)?.cliente ?? null;

  protected readonly editando = this.existente !== null;
  protected readonly salvando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected dados: ClienteRequest = {
    nome: this.existente?.nome ?? '',
    documento: this.existente?.documento ?? '',
    email: this.existente?.email ?? '',
  };

  protected salvar(): void {
    if (this.salvando()) {
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);

    const requisicao = this.existente
      ? this.servico.atualizar(this.existente.id, this.dados)
      : this.servico.criar(this.dados);

    requisicao.subscribe({
      next: () => {
        this.salvando.set(false);
        this.referencia.close(true);
      },
      error: (falha: HttpErrorResponse) => {
        this.salvando.set(false);
        this.erro.set(this.descreverFalha(falha));
      },
    });
  }

  protected cancelar(): void {
    this.referencia.close(false);
  }

  private descreverFalha(falha: HttpErrorResponse): string {
    // 409 é conflito de documento duplicado — o dado está válido, quem conflita
    // é o estado do sistema. A mensagem do backend já explica.
    if (falha.status === 409) {
      return falha.error?.message ?? 'Já existe um cliente com este documento.';
    }

    if (falha.status === 400 && falha.error?.errors) {
      return Object.values(falha.error.errors as Record<string, string[]>)
        .flat()
        .join(' ');
    }

    return falha.error?.message ?? 'Não foi possível salvar o cliente.';
  }
}
