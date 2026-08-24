using ConexaoDinamica.Application.Dtos.AdminDtos;
using FluentValidation;

namespace ConexaoDinamica.Application.Validador.AdminValidadores
{
    /// <summary>
    /// Registrado automaticamente pelo AddValidatorsFromAssemblyContaining, que
    /// varre o assembly inteiro — não é preciso tocar no FluentValidatorExtension.
    ///
    /// Valida forma, não conectividade: se o host existe ou se a senha está certa
    /// é o que o endpoint de teste responde. Aqui só barramos o que nem chega a
    /// fazer sentido tentar.
    /// </summary>
    public class ConexaoPostgresRequestValidator : AbstractValidator<ConexaoPostgresRequest>
    {
        public ConexaoPostgresRequestValidator()
        {
            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("Host é obrigatório")
                .MaximumLength(255).WithMessage("Host deve ter no máximo 255 caracteres");

            RuleFor(x => x.Porta)
                .InclusiveBetween(1, 65535).WithMessage("Porta deve estar entre 1 e 65535");

            RuleFor(x => x.Database)
                .NotEmpty().WithMessage("Nome do banco é obrigatório")
                .MaximumLength(63).WithMessage("Nome do banco deve ter no máximo 63 caracteres");

            RuleFor(x => x.Usuario)
                .NotEmpty().WithMessage("Usuário é obrigatório")
                .MaximumLength(63).WithMessage("Usuário deve ter no máximo 63 caracteres");

            // Senha não é obrigatória: Postgres aceita autenticação trust ou peer,
            // em que o usuário conecta sem senha nenhuma.
        }
    }
}
