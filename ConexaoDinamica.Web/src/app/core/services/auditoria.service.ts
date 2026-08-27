import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  EventoAuditoria,
  FiltroAuditoria,
  ResultadoPaginado,
} from '../models/conexao.model';

/**
 * Consulta da trilha de auditoria. Exige role Administrador.
 */
@Injectable({ providedIn: 'root' })
export class AuditoriaService {
  private readonly http = inject(HttpClient);
  private static readonly BASE = '/api/v1/admin/auditoria';

  consultar(filtro: FiltroAuditoria): Observable<ResultadoPaginado<EventoAuditoria>> {
    return this.http.get<ResultadoPaginado<EventoAuditoria>>(AuditoriaService.BASE, {
      params: this.montarParametros(filtro),
    });
  }

  /**
   * Baixa a planilha com TODOS os eventos que casam com o filtro.
   *
   * Vai ao servidor mesmo com a tela já carregada, e não é desperdício: a tela
   * tem uma página, e o backend limita a 100 por requisição. Montar a planilha
   * aqui exigiria varrer a paginação em laço — e, mais importante, a exportação
   * precisa ser registrada na própria trilha, o que só o servidor consegue fazer.
   *
   * Paginação é omitida de propósito; o endpoint de exportação a ignora.
   */
  exportar(filtro: FiltroAuditoria): Observable<Blob> {
    const { pagina: _pagina, tamanhoPagina: _tamanhoPagina, ...criterios } = filtro;

    return this.http.get(`${AuditoriaService.BASE}/exportar`, {
      params: this.montarParametros(criterios),
      // Sem isto o Angular tentaria interpretar o XLSX como JSON e a
      // requisição falharia no parse, mesmo com o arquivo tendo chegado inteiro.
      responseType: 'blob',
    });
  }

  /**
   * Só envia o que foi preenchido: parâmetros vazios chegariam como string
   * vazia e o backend os trataria como filtro, devolvendo nada.
   */
  private montarParametros(filtro: Partial<FiltroAuditoria>): HttpParams {
    let params = new HttpParams();

    for (const [chave, valor] of Object.entries(filtro)) {
      if (valor !== undefined && valor !== null && valor !== '') {
        params = params.set(chave, String(valor));
      }
    }

    return params;
  }

  obterTiposEntidade(): Observable<string[]> {
    return this.http.get<string[]>(`${AuditoriaService.BASE}/tipos-entidade`);
  }
}
