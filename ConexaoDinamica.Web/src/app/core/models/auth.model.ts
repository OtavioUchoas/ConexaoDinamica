/** Corpo aceito por POST /api/v1/login */
export interface LoginRequest {
  email: string;
  senha: string;
}

/** Corpo aceito por POST /api/v1/admin/login */
export interface AdminLoginRequest {
  /** Aceita username OU email — o backend compara com os dois. */
  login: string;
  senha: string;
}

/** Resposta de POST /api/v1/login */
export interface LoginResponse {
  token: string;
  nome: string;
  email: string;
  usuarioId: number;
}

/**
 * Resposta de POST /api/v1/admin/login.
 *
 * Difere de LoginResponse de propósito: o admin de bootstrap não existe no
 * banco, então não tem usuarioId. Em compensação devolve o perfil.
 */
export interface AdminLoginResponse {
  token: string;
  nome: string;
  email: string;
  perfil: string;
}

/**
 * Sessão normalizada, como o frontend enxerga.
 *
 * Os dois endpoints de login devolvem formatos diferentes; converter para um
 * único tipo aqui evita espalhar essa diferença pelas telas.
 */
export interface Sessao {
  token: string;
  nome: string;
  email: string;
  perfil: string;
  /** Ausente quando a sessão veio do admin de bootstrap. */
  usuarioId?: number;
}

export const PERFIL_ADMINISTRADOR = 'Administrador';
