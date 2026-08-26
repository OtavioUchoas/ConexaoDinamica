import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  PedidoRequest,
  PedidoResponse,
  ResultadoPaginado,
} from '../models/conexao.model';

@Injectable({ providedIn: 'root' })
export class PedidoService {
  private readonly http = inject(HttpClient);
  private static readonly BASE = '/api/v1/pedidos';

  listar(busca: string, pagina: number, tamanhoPagina: number): Observable<ResultadoPaginado<PedidoResponse>> {
    let params = new HttpParams().set('pagina', pagina).set('tamanhoPagina', tamanhoPagina);

    if (busca.trim()) {
      params = params.set('busca', busca.trim());
    }

    return this.http.get<ResultadoPaginado<PedidoResponse>>(PedidoService.BASE, { params });
  }

  obterPorId(id: number): Observable<PedidoResponse> {
    return this.http.get<PedidoResponse>(`${PedidoService.BASE}/${id}`);
  }

  criar(dados: PedidoRequest): Observable<PedidoResponse> {
    return this.http.post<PedidoResponse>(PedidoService.BASE, dados);
  }

  atualizar(id: number, dados: PedidoRequest): Observable<PedidoResponse> {
    return this.http.put<PedidoResponse>(`${PedidoService.BASE}/${id}`, dados);
  }

  remover(id: number): Observable<void> {
    return this.http.delete<void>(`${PedidoService.BASE}/${id}`);
  }
}
