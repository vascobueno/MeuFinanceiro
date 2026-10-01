# 05 — Regras de Negócio

## Descrição
Autocomplete por `StartsWith(prefix)`. Priorizar frequência de uso e uso recente.

## Recorrência
Alteração a partir de uma data cria nova versão. Ocorrências históricas e conciliadas não mudam.

## Parcelamento
Modo A: principal + quantidade.
Modo B: valor da parcela + quantidade.
A soma deve fechar exatamente o principal.

## Transferência
Entre contas próprias não é receita nem despesa.

## Pagamento de cartão
Pagar a fatura é **movimentação, não despesa** — mesma razão da Transferência. A compra no cartão
já foi lançada como despesa; contar o pagamento de novo somaria o mesmo dinheiro duas vezes em todo
relatório de gastos. Por isso o lançamento do pagamento usa `TransactionType.CardPayment`, que as
agregações de despesa (que filtram `Expense`) excluem naturalmente.

O pagamento pode sair de qualquer conta corrente, ou ser marcado como pago por outra forma — aí não
afeta conta nenhuma, só abate a fatura. A conta pode ser trocada depois, e o débito acompanha.

A fatura de um ciclo é calculada a partir dos lançamentos do cartão na janela do ciclo, definida
pelo dia de fechamento. **Ela inclui lançamentos pendentes**, ao contrário do saldo de conta: os
lançamentos nascem `Pending`, e excluí-los deixaria a fatura zerada. No cartão, "pendente" quer
dizer não conferido, não "ainda não gastei".

Pagamento parcial é aceito, e pagar a mais deixa o em aberto negativo — é crédito a favor, não erro.

O fechamento de **uma** fatura pode ser ajustado, quando a operadora antecipa ou adia aquele mês. O
ajuste vale só para o mês escolhido e tem de ser uma data **dentro** dele. Por isso a fatura é
identificada pelo mês, e não pela data de fechamento: assim um ajuste não desvincula os pagamentos
já registrados.

**O vencimento é independente do fechamento.** Ele sai do dia do cadastro aplicado ao fechamento
*padrão* do mês, e **não se move** quando o fechamento é ajustado — a operadora antecipa o
fechamento, mas o dia de vencimento costuma ser fixo. Também pode ser ajustado por fatura: tem de
ser depois do fechamento efetivo e no máximo 90 dias após ele (limite só de sanidade contra erro de
digitação, já que o vencimento não define a janela do ciclo).

Uma única exceção move o vencimento sozinho, e é correção, não preferência: **uma fatura não pode
vencer antes de fechar**. Se o fechamento for adiado para depois do vencimento, este avança para a
próxima ocorrência do dia. Nesse caso ele não é marcado como "ajustado", porque o usuário não
ajustou nada.

Ajustar o fechamento para cima ou depois de um vencimento **já ajustado** é recusado, não empurrado:
o valor foi escolhido pelo usuário, e mudá-lo em silêncio é pior que pedir para ajustar na ordem
certa.

## Investimentos
Aplicação reduz conta e aumenta investimento.
Resgate aumenta conta e reduz investimento.
Rendimento é evento separado.

## Saldos
Saldo de conta não inclui investimentos.
Patrimônio consolidado pode somar os dois somente para apresentação.

## Projeção
Pode considerar lançamentos futuros, recorrências, parcelas, faturas, aplicações, resgates e rendimentos quando confiáveis. Nunca inventar rentabilidade.
