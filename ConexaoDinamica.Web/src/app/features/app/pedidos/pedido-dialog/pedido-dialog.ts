import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

import { PedidoService } from '../../../../core/services/pedido.service';
import { ClienteService } from '../../../../core/services/cliente.service';
import {
  ClienteResponse,
  ItemPedidoRequest,
  PedidoResponse,
  StatusPedido,
} from '../../../../core/models/conexao.model';

/**
 * Cadastro e edição de pedido, com as linhas de itens.
 *
 * ── Sobre os ids dos itens ───────────────────────────────────────────────────
 * Cada linha carrega o id do item quando ele já existe. Isso não é detalhe de
 * implementação: é o que permite ao servidor ATUALIZAR o item em vez de apagar
 * e recriar — e é o que faz a auditoria registrar "Quantidade: 2 -> 5" em vez
 * de "removidos 3 itens, adicionados 3 itens" a cada gravação.
 */
@Component({
  selector: 'app-pedido-dialog',
  imports: [
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
  ],
  templateUrl: './pedido-dialog.html',
  styleUrl: './pedido-dialog.scss',
})
export class PedidoDialog {
  private readonly servico = inject(PedidoService);
  private readonly clientes = inject(ClienteService);
  private readonly referencia = inject(MatDialogRef<PedidoDialog>);

  private readonly existente: PedidoResponse | null = inject(MAT_DIALOG_DATA)?.pedido ?? null;

  protected readonly editando = this.existente !== null;
  protected readonly salvando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected readonly listaClientes = signal<ClienteResponse[]>([]);

  protected readonly status: StatusPedido[] = ['Rascunho', 'Confirmado', 'Enviado', 'Cancelado'];

  protected numero = this.existente?.numero ?? '';
  protected clienteId: number | null = this.existente?.clienteId ?? null;
  protected statusSelecionado: StatusPedido = this.existente?.status ?? 'Rascunho';

  protected readonly itens = signal<ItemPedidoRequest[]>(
    this.existente?.itens.map((i) => ({
      id: i.id,
      descricao: i.descricao,
      quantidade: i.quantidade,
      precoUnitario: i.precoUnitario,
    })) ?? [{ id: null, descricao: '', quantidade: 1, precoUnitario: 0 }],
  );

  /**
   * Prévia do total. O valor gravado é sempre o que o SERVIDOR calcula — este
   * aqui existe só para o usuário conferir antes de salvar.
   */
  protected readonly totalPrevisto = computed(() =>
    this.itens().reduce((soma, i) => soma + (i.quantidade || 0) * (i.precoUnitario || 0), 0),
  );

  constructor() {
    // tamanhoPagina alto porque é um select: paginar aqui esconderia clientes
    // sem que o usuário perceba. Com muitos cadastros, o certo seria um campo
    // com busca no servidor.
    this.clientes.listar('', 1, 100).subscribe({
      next: (resultado) => this.listaClientes.set(resultado.itens),
      error: () => this.listaClientes.set([]),
    });
  }

  protected adicionarItem(): void {
    this.itens.update((atual) => [
      ...atual,
      { id: null, descricao: '', quantidade: 1, precoUnitario: 0 },
    ]);
  }

  protected removerItem(indice: number): void {
    this.itens.update((atual) => atual.filter((_, i) => i !== indice));
  }

  protected subtotal(item: ItemPedidoRequest): number {
    return (item.quantidade || 0) * (item.precoUnitario || 0);
  }

  protected salvar(): void {
    if (this.salvando()) {
      return;
    }

    if (this.clienteId === null) {
      this.erro.set('Selecione um cliente.');
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);

    const dados = {
      numero: this.numero,
      clienteId: this.clienteId,
      status: this.statusSelecionado,
      itens: this.itens(),
    };

    const requisicao = this.existente
      ? this.servico.atualizar(this.existente.id, dados)
      : this.servico.criar(dados);

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
    if (falha.status === 409) {
      return falha.error?.message ?? 'Já existe um pedido com este número.';
    }

    if (falha.status === 400) {
      if (falha.error?.errors) {
        // As mensagens dos itens vêm com a chave "Itens[0].Quantidade", o que
        // já indica a linha problemática.
        return Object.entries(falha.error.errors as Record<string, string[]>)
          .map(([campo, mensagens]) => `${campo}: ${mensagens.join(' ')}`)
          .join(' · ');
      }

      return falha.error?.message ?? 'Dados inválidos.';
    }

    return falha.error?.message ?? 'Não foi possível salvar o pedido.';
  }
}
