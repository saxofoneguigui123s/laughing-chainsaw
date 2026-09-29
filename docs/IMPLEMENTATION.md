# Decisões de implementação

## Fonte única e geração reproduzível

A tabela fornecida pelo proprietário foi transcrita em `tools/build_nightfall_content.py`. O script usa uma namespace UUID fixa para derivar GUIDs determinísticos, gera os arquivos de asset, tabela de loot, documentação de administração e o ZIP de distribuição. Não há GUID aleatório que mude a cada build.

## Balanceamento complementar

Os valores fornecidos (dano, peso, raridade, durabilidade, proteção, vida, velocidade, combustível e passageiros) não foram substituídos. Para campos que não estavam na tabela, foram definidos perfis coerentes por classe:

- pistolas, SMGs, fuzis, DMRs, precisão, escopetas e metralhadoras recebem alcance, ação, cadência, slots, ruído, modificadores e munição compatível;
- armas históricas recebem alcance, custo de stamina e força de ataque por categoria;
- coletes recebem capacidade de inventário e penalidade de movimento baseada no peso;
- veículos recebem tamanho de porta-malas e lista de upgrades planejados.

Esses campos extras são ajustes explícitos no catálogo e podem ser modificados sem perda dos números originais.

## Raridade e loot

A distribuição segue o sistema solicitado:

| Raridade | Peso de loot | Destino padrão |
|---|---:|---|
| Comum | 60 | casas, lojas e veículos abandonados |
| Incomum | 25 | polícia/militar |
| Raro | 10 | áreas especiais |
| Épico | 4 | bases, depósitos e eventos |
| Lendário | 1 | eventos e endgame |

Os pesos e referências GUID ficam em `NightfallSurvivalExpansion/LootTables.json` para que o dono conecte às tabelas de spawn específicas do mapa.
