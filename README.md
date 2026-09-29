# Nightfall Survival Expansion

Conteúdo de sobrevivência para **Unturned / U3-SDK v3.26.3.12**.

O repositório agora contém um pacote de conteúdo orientado por dados em [`NightfallSurvivalExpansion/`](NightfallSurvivalExpansion/):

- **48 armas modernas**;
- **39 armas antigas**;
- **15 coletes modernos**;
- **47 veículos modernos**;
- **12 carregadores/feixes de munição de suporte**.

São **161 definições de runtime** no total, com GUIDs estáveis, IDs legados reservados, raridade, durabilidade, peso, dano/proteção, combustível, vida e passageiros. Consulte [`NightfallSurvivalExpansion/README.md`](NightfallSurvivalExpansion/README.md) e [`docs/INSTALL.md`](docs/INSTALL.md).

## Gerar o pacote

```bash
python3 tools/build_nightfall_content.py
python3 tests/test_nightfall_content.py
```

O comando atualiza as definições e cria o pacote distribuível em `release/Nightfall-Survival-Expansion-content-v3.26.3.12.3.zip`.

## Nota sobre os modelos Unity

As definições `.dat` e as fichas de cada item estão completas. Unturned também exige os prefabs/modelos Unity correspondentes em cada asset bundle (`Item`, `Equip`, `Vest`, `Vehicle`, etc.). Como o ZIP do SDK informado é um binário externo e não está versionado neste repositório, o pacote deixa esses requisitos explícitos em `AssetRequirements.md` dentro de cada pasta de asset. Reenvie/anexe o projeto U3-SDK caso queira que os prefabs e bundles finais sejam inseridos diretamente nele.
