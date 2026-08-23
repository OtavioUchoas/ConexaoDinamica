using System;
using System.Collections.Generic;
using System.Text;

namespace ConexaoDinamica.Domain.Entidades.Autenticacao
{
    public class TokenRequest
    {
        public int UsuarioId { get; set; }
        public string Nome { get; set; } = string.Empty;
    }
}
