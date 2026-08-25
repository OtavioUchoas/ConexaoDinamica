import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  AplicarMigrationsResponse,
  ConexaoMongoRequest,
  ConexaoMongoResponse,
  ConexaoPostgresRequest,
  ConexaoPostgresResponse,
  StatusMigrationsResponse,
  TesteConexaoResponse,
} from '../models/conexao.model';

/**
 * Endpoints do AdminCenter. Todos exigem role Administrador.
 *
 * Os metodos de teste NAO tratam falha de conexao como erro HTTP: o backend
 * responde 200 com { sucesso: false }, e cabe a tela exibir a mensagem no
 * proprio formulario, em vez de disparar o tratamento global de erros.
 */
@Injectable({ providedIn: 'root' })
export class ConexaoService {
  private readonly http = inject(HttpClient);
  private static readonly BASE = '/api/v1/admin/conexao';

  obterPostgres(): Observable<ConexaoPostgresResponse> {
    return this.http.get<ConexaoPostgresResponse>(`${ConexaoService.BASE}/postgres`);
  }

  testarPostgres(dados: ConexaoPostgresRequest): Observable<TesteConexaoResponse> {
    return this.http.post<TesteConexaoResponse>(`${ConexaoService.BASE}/postgres/testar`, dados);
  }

  salvarPostgres(dados: ConexaoPostgresRequest): Observable<ConexaoPostgresResponse> {
    return this.http.put<ConexaoPostgresResponse>(`${ConexaoService.BASE}/postgres`, dados);
  }

  obterStatusMigrations(): Observable<StatusMigrationsResponse> {
    return this.http.get<StatusMigrationsResponse>(`${ConexaoService.BASE}/postgres/migrations`);
  }

  aplicarMigrations(): Observable<AplicarMigrationsResponse> {
    return this.http.post<AplicarMigrationsResponse>(
      `${ConexaoService.BASE}/postgres/migrations`,
      {},
    );
  }

  obterMongo(): Observable<ConexaoMongoResponse> {
    return this.http.get<ConexaoMongoResponse>(`${ConexaoService.BASE}/mongo`);
  }

  testarMongo(dados: ConexaoMongoRequest): Observable<TesteConexaoResponse> {
    return this.http.post<TesteConexaoResponse>(`${ConexaoService.BASE}/mongo/testar`, dados);
  }

  salvarMongo(dados: ConexaoMongoRequest): Observable<ConexaoMongoResponse> {
    return this.http.put<ConexaoMongoResponse>(`${ConexaoService.BASE}/mongo`, dados);
  }
}
