using ConexaoDinamica.Application.Dtos.UsuariosDtos;
using FluentValidation;

namespace ConexaoDinamica.Application.Validador.UsuariosValidadores
{
    public class CadastroUsuarioValidator : AbstractValidator<CadastroUsuario>
    {
        public CadastroUsuarioValidator()
        {
            RuleFor(x => x.Nome)
                .NotEmpty().WithMessage("Nome é obrigatório")
                .MinimumLength(3).WithMessage("Nome deve ter no mínimo 3 caracteres");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email é obrigatório")
                .EmailAddress().WithMessage("Email inválido");

            RuleFor(x => x.Senha)
                .NotEmpty().WithMessage("Senha é obrigatória")
                .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres")
                .MaximumLength(128).WithMessage("Senha deve ter no máximo 128 caracteres")
                .Matches(@"[A-Z]").WithMessage("Senha deve conter ao menos uma letra maiúscula")
                .Matches(@"[a-z]").WithMessage("Senha deve conter ao menos uma letra minúscula")
                .Matches(@"[0-9]").WithMessage("Senha deve conter ao menos um número")
                .Matches(@"[!@#$%^&*]").WithMessage("Senha deve conter ao menos um caractere especial (!@#$%^&*)");
        }
    }
}
