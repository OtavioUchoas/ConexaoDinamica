using ConexaoDinamica.Application.Dtos.ClientesDtos;
using FluentValidation;

namespace ConexaoDinamica.Application.Validador.ClientesValidadores
{
    public class ClienteRequestValidator : AbstractValidator<ClienteRequest>
    {
        public ClienteRequestValidator()
        {
            RuleFor(x => x.Nome)
                .NotEmpty().WithMessage("Nome é obrigatório")
                .MaximumLength(150).WithMessage("Nome deve ter no máximo 150 caracteres");

            RuleFor(x => x.Documento)
                .NotEmpty().WithMessage("Documento é obrigatório")
                .MaximumLength(20).WithMessage("Documento deve ter no máximo 20 caracteres");

            // Só valida formato quando informado: o e-mail é opcional.
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("E-mail inválido")
                .MaximumLength(150).WithMessage("E-mail deve ter no máximo 150 caracteres")
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
        }
    }
}
