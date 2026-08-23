using ConexaoDinamica.Application.Dtos.AdminDtos;
using FluentValidation;

namespace ConexaoDinamica.Application.Validador.AdminValidadores
{
    public class AdminLoginRequestValidator : AbstractValidator<AdminLoginRequest>
    {
        public AdminLoginRequestValidator()
        {
            RuleFor(x => x.Login)
                .NotEmpty().WithMessage("Login é obrigatório");

            RuleFor(x => x.Senha)
                .NotEmpty().WithMessage("Senha é obrigatória");
        }
    }
}
