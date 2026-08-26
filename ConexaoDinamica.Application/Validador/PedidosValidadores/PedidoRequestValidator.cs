using ConexaoDinamica.Application.Dtos.PedidosDtos;
using FluentValidation;

namespace ConexaoDinamica.Application.Validador.PedidosValidadores
{
    public class PedidoRequestValidator : AbstractValidator<PedidoRequest>
    {
        public PedidoRequestValidator()
        {
            RuleFor(x => x.Numero)
                .NotEmpty().WithMessage("Número é obrigatório")
                .MaximumLength(30).WithMessage("Número deve ter no máximo 30 caracteres");

            RuleFor(x => x.ClienteId)
                .GreaterThan(0).WithMessage("Cliente é obrigatório");

            RuleFor(x => x.Itens)
                .NotEmpty().WithMessage("Informe ao menos um item");

            // Valida cada item da coleção individualmente, com o índice na
            // mensagem — sem isso, o erro apontaria para "Itens" e o usuário não
            // saberia qual linha corrigir.
            RuleForEach(x => x.Itens).ChildRules(item =>
            {
                item.RuleFor(i => i.Descricao)
                    .NotEmpty().WithMessage("Descrição do item é obrigatória")
                    .MaximumLength(200).WithMessage("Descrição deve ter no máximo 200 caracteres");

                item.RuleFor(i => i.Quantidade)
                    .GreaterThan(0).WithMessage("Quantidade deve ser maior que zero");

                item.RuleFor(i => i.PrecoUnitario)
                    .GreaterThanOrEqualTo(0).WithMessage("Preço não pode ser negativo");
            });
        }
    }
}
