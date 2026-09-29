# Instalação e integração no U3-SDK

## O que este pacote entrega

`NightfallSurvivalExpansion/` é o conteúdo de jogo pronto para integrar:

- `catalog.json` é a fonte de verdade de 150 itens/veículos solicitados e 12 munições auxiliares;
- cada diretório de asset contém um `.dat` compatível com **U3-SDK v3.26.3.12**, `English.dat` e uma ficha de requisitos;
- `GUID_REGISTRY.md` fornece GUIDs estáveis e os IDs legados reservados;
- `LootTables.json` já separa os assets em loot civil, policial/militar, área especial e evento/endgame.

A definição usa GUID como chave principal. Isso evita conflitos com mods instalados; prefira GUIDs em tabelas de spawn, NPCs e integrações de servidor. Os IDs `62000–62511` são apenas um fallback e precisam ser conferidos contra os demais mods antes de publicar.

## Passos no projeto Unity

1. Extraia/abra o projeto U3-SDK **v3.26.3.12** que será o destino.
2. Copie `NightfallSurvivalExpansion/Items` e `NightfallSurvivalExpansion/Vehicles` para a área de conteúdo usada pelo pipeline existente do projeto.
3. Para cada pasta, crie o prefab e o bundle correspondente. O arquivo `AssetRequirements.md` lista os objetos que o loader espera:
   - arma de fogo: `Item`, `Equip`, `Projectile` e os `Hook_*` dos modificadores;
   - arma corpo a corpo: `Item` e `Equip` com animações/áudios de ataque;
   - colete: `Item` e `Vest` com malha riggada para roupa de Unturned;
   - veículo: `Vehicle` com `Rigidbody`, `Seats/Seat_0…`, `Tires` e `Clip`.
4. Gere os asset bundles pelo mesmo processo empregado pelos outros conteúdos do projeto (no U3-SDK, o **Window → Unturned → Bundle Tool** pode ser usado para bundles legados).
5. Instale/publice os bundles e valide no log do jogo que não há erro de asset ausente.
6. Remova `Bypass_Hash_Verification True` dos `.dat` antes de uma publicação de produção. A flag só existe para facilitar o trabalho de integração da fonte.

## Dados e conversões implementadas

- **Dano, raridade e durabilidade** de armas correspondem à tabela do projeto. `Quality_Max` recebe a durabilidade solicitada e cada arma preserva os estados Novo / Usado / Danificado / Quebrado na localização.
- **Peso** é preservado em `catalog.json` e na descrição. O formato de item do Unturned não possui um campo de peso nativo; o tamanho de inventário e o multiplicador de mobilidade foram usados apenas como balanceamento complementar.
- **Coletes:** o Unturned representa armadura como multiplicador de dano recebido. Por isso, 25% de absorção é exportado como `Armor 0.75`, 80% como `Armor 0.20`, etc. A degradação usa a qualidade nativa da roupa.
- **Veículos:** `Health`, `Fuel` e `Fuel_Max` recebem os valores da tabela. A velocidade de projeto em km/h fica preservada no catálogo e `Speed_Max` é convertido para m/s. A contagem de assentos no prefab deve ser igual à coluna `passengers` de cada veículo.
- **Bote inflável:** combustível zero e a nota `Não utiliza combustível`.

## Teste recomendado

Antes de publicar, faça pelo menos estes testes para cada família:

1. Spawn por GUID de uma arma, colete e veículo.
2. Equipar, recarregar e descarregar uma arma de cada tipo.
3. Confirmar que o colete reduz dano no tronco e perde qualidade quando a opção de dano a roupas está ativa.
4. Dirigir cada tipo de veículo, conferir número de assentos, combustível, vida, pneus e porta-malas.
5. Validar que raridades e tabelas de loot seguem Comum → Incomum → Raro → Épico → Lendário.

> Modelos, texturas, sons e animações não foram fornecidos na especificação. As configurações não inventam ou redistribuem assets de terceiros: cada asset documenta exatamente o prefab que ainda precisa ser criado/importado no projeto Unity.
