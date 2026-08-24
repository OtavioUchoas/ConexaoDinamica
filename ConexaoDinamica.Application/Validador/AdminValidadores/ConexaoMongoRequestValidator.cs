using ConexaoDinamica.Application.Dtos.AdminDtos;
using FluentValidation;

namespace ConexaoDinamica.Application.Validador.AdminValidadores
{
    public class ConexaoMongoRequestValidator : AbstractValidator<ConexaoMongoRequest>
    {
        public ConexaoMongoRequestValidator()
        {
            RuleFor(x => x.Host)
                .NotEmpty().WithMessage("Host é obrigatório")
                .MaximumLength(255).WithMessage("Host deve ter no máximo 255 caracteres");

            RuleFor(x => x.Porta)
                .InclusiveBetween(1, 65535).WithMessage("Porta deve estar entre 1 e 65535");

            RuleFor(x => x.Database)
                .NotEmpty().WithMessage("Nome do banco é obrigatório")
                .MaximumLength(63).WithMessage("Nome do banco deve ter no máximo 63 caracteres");

            // AuthSource só faz sentido quando há usuário. Exigi-lo sempre
            // impediria configurar um Mongo local sem autenticação.
            RuleFor(x => x.AuthSource)
                .NotEmpty().WithMessage("AuthSource é obrigatório quando há usuário informado")
                .When(x => !string.IsNullOrWhiteSpace(x.Usuario));
        }
    }
}
