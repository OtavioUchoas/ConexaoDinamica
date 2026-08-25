export interface ConexaoPostgresRequest {
  host: string;
  porta: number;
  database: string;
  usuario: string;
  senha: string;
}

/**
 * Note que não existe campo de senha: o backend devolve apenas se existe uma
 * salva. É o motivo de request e response serem tipos separados.
 */
export interface ConexaoPostgresResponse {
  host: string;
  porta: number;
  database: string;
  usuario: string;
  senhaDefinida: boolean;
  estaCompleta: boolean;
  dataAtualizacao: string;
}

export interface ConexaoMongoRequest {
  host: string;
  porta: number;
  database: string;
  usuario: string;
  senha: string;
  authSource: string;
}

export interface ConexaoMongoResponse {
  host: string;
  porta: number;
  database: string;
  usuario: string;
  authSource: string;
  senhaDefinida: boolean;
  estaCompleta: boolean;
  dataAtualizacao: string;
}

/**
 * Resultado do "Testar conexão".
 *
 * Chega com HTTP 200 mesmo quando falha: a pergunta era "dá para conectar?" e
 * "não" é resposta válida. Quem decide o que mostrar é o campo sucesso, não o
 * status HTTP.
 */
export interface TesteConexaoResponse {
  sucesso: boolean;
  mensagem: string;
  tempoMs: number;
  versaoServidor: string | null;
}

export interface StatusMigrationsResponse {
  configurado: boolean;
  conseguiuConectar: boolean;
  erro: string | null;
  aplicadas: string[];
  pendentes: string[];
}

export interface SuperUsuarioCriadoResponse {
  email: string;
  senhaProvisoria: string;
  aviso: string;
}

export interface AplicarMigrationsResponse {
  sucesso: boolean;
  mensagem: string;
  aplicadas: string[];
  /** Só vem preenchido na primeira vez, quando o super admin é criado. */
  superUsuario: SuperUsuarioCriadoResponse | null;
}

/** Corpo do 503 devolvido pelo ModoSetupMiddleware. */
export interface SetupPendente {
  statusCode: number;
  setupRequired: true;
  conexoes: { postgres: boolean; mongo: boolean };
  message: string;
}
