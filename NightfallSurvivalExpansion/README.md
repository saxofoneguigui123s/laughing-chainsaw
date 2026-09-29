# Nightfall Survival Expansion — conteúdo v3.26.3.12.3

Pacote de conteúdo orientado por dados para **U3-SDK v3.26.3.12**.

## Incluído

- 48 armas modernas;
- 39 armas antigas;
- 15 coletes modernos com absorção convertida para o multiplicador `Armor` nativo;
- 47 veículos com vida, velocidade, combustível e passageiros;
- 12 carregadores/feixes de munição de apoio para as armas de fogo e projéteis.

Total: **161 definições de runtime**. Cada entrada tem GUID estável, fallback de ID legado, raridade, localização PT-BR e ficha de requisitos do prefab.

## Arquivos importantes

- `catalog.json`: fonte completa e auditável dos atributos do jogo.
- `Items/**/<item>.dat`: definições U3-SDK geradas.
- `Vehicles/**/<vehicle>.dat`: definições U3-SDK geradas.
- `LootTables.json`: tabelas de loot por nível de raridade, usando GUIDs.
- `GUID_REGISTRY.md`: lista de comandos/IDs para administração.
- `docs/INSTALL.md` no repositório: instruções para criar os asset bundles Unity exigidos pelo SDK.

> As definições incluem `Bypass_Hash_Verification` porque são entregues como fontes de conteúdo. Remova essa linha quando publicar bundles finais, para manter a verificação de integridade no servidor.
