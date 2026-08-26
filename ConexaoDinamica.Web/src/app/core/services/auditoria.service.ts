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
    let params = new HttpParams();

    // Só envia o que foi preenchido: parâmetros vazios chegariam como string
    // vazia e o backend os trataria como filtro, devolvendo nada.
    for (const [chave, valor] of Object.entries(filtro)) {
      if (valor !== undefined && valor !== null && valor !== '') {
        params = params.set(chave, String(valor));
      }
    }

    return this.http.get<ResultadoPaginado<EventoAuditoria>>(AuditoriaService.BASE, { params });
  }

  obterTiposEntidade(): Observable<string[]> {
    return this.http.get<string[]>(`${AuditoriaService.BASE}/tipos-entidade`);
  }
}
