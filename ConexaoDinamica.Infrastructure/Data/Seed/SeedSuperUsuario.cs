using System.Security.Cryptography;
using ConexaoDinamica.Domain.Entidades.Usuarios;
using ConexaoDinamica.Domain.Enums;
using ConexaoDinamica.Infrastructure.Data.AppDBsContext;
using Microsoft.EntityFrameworkCore;

namespace ConexaoDinamica.Infrastructure.Data.Seed
{
    /// <summary>
    /// Cria o super administrador no Postgres depois das migrations.
    ///
    /// ── Por que em código e não com HasData ──────────────────────────────────
    /// HasData exige valores determinísticos em tempo de design, e o hash BCrypt
    /// não é: o salt muda a cada chamada, então o EF veria o modelo mudando a cada
    /// "migrations add" e passaria a gerar migrations espúrias de UPDATE no seed.
    ///
    /// Há um problema pior. A coluna Id é IDENTITY BY DEFAULT: um INSERT com id
    /// explícito é aceito pelo Postgres, mas NÃO avança a sequence. O primeiro
    /// usuário cadastrado depois puxaria o mesmo id e receberia violação de chave
    /// primária. Criando pela aplicação, a sequence é usada normalmente.
    ///
    /// ── Por que a senha é gerada ─────────────────────────────────────────────
    /// Uma senha padrão no código seria idêntica em toda instalação, ficaria no
    /// repositório e sobreviveria esquecida. Gerada aleatoriamente, ela aparece
    /// uma única vez na resposta do setup e só existe no banco como hash.
    /// </summary>
    internal static class SeedSuperUsuario
    {
        /// <summary>
        /// Cria o super administrador se ainda não existir.
        /// Idempotente: nas execuções seguintes retorna null sem tocar no banco.
        /// </summary>
        /// <returns>A senha gerada, apenas quando o usuário acabou de ser criado.</returns>
        public static async Task<string?> GarantirAsync(
            AppDbContext contexto,
            string email,
            string nome,
            CancellationToken cancellationToken = default)
        {
            var jaExiste = await contexto.Usuarios
                .AnyAsync(u => u.Email == email, cancellationToken);

            if (jaExiste)
                return null;

            var senha = GerarSenha();

            contexto.Usuarios.Add(new Usuario
            {
                Nome = nome,
                Email = email,
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(senha),
                Perfil = PerfilUsuario.Administrador,
                DataCriacao = DateTime.UtcNow
            });

            await contexto.SaveChangesAsync(cancellationToken);

            return senha;
        }

        /// <summary>
        /// Senha aleatória de 20 caracteres.
        ///
        /// Usa RandomNumberGenerator, e não Random: este último é previsível a
        /// partir da semente, o que tornaria a credencial inicial adivinhável.
        /// O alfabeto evita caracteres ambíguos (O/0, l/1) porque essa senha será
        /// lida da tela e digitada por uma pessoa.
        /// </summary>
        private static string GerarSenha()
        {
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*";
            const int tamanho = 20;

            var caracteres = new char[tamanho];

            for (var i = 0; i < tamanho; i++)
                caracteres[i] = alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];

            return new string(caracteres);
        }
    }
}
