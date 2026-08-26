import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  ClienteRequest,
  ClienteResponse,
  ResultadoPaginado,
} from '../models/conexao.model';

@Injectable({ providedIn: 'root' })
export class ClienteService {
  private readonly http = inject(HttpClient);
  private static readonly BASE = '/api/v1/clientes';

  listar(busca: string, pagina: number, tamanhoPagina: number): Observable<ResultadoPaginado<ClienteResponse>> {
    let params = new HttpParams()
      .set('pagina', pagina)
      .set('tamanhoPagina', tamanhoPagina);

    // Busca vazia é omitida: enviá-la como string vazia faria o backend tratar
    // como filtro e não retornar nada.
    if (busca.trim()) {
      params = params.set('busca', busca.trim());
    }

    return this.http.get<ResultadoPaginado<ClienteResponse>>(ClienteService.BASE, { params });
  }

  obterPorId(id: number): Observable<ClienteResponse> {
    return this.http.get<ClienteResponse>(`${ClienteService.BASE}/${id}`);
  }

  criar(dados: ClienteRequest): Observable<ClienteResponse> {
    return this.http.post<ClienteResponse>(ClienteService.BASE, dados);
  }

  atualizar(id: number, dados: ClienteRequest): Observable<ClienteResponse> {
    return this.http.put<ClienteResponse>(`${ClienteService.BASE}/${id}`, dados);
  }

  remover(id: number): Observable<void> {
    return this.http.delete<void>(`${ClienteService.BASE}/${id}`);
  }
}
