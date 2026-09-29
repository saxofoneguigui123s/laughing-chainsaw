# Unturned SDK - Correção Unity 6 / Fix for Unity 6

> **PT-BR:** Este repositório contém a correção completa para os 50 erros de shader `HLSLSupport.cginc` e warnings `webgpu` do Unturned SDK Nightfall Survival Expansion v3.26.3.12.2 quando compilado no Unity 6 ou Unity 2021+.

> **EN:** This repo contains the full fix for 50 shader errors `HLSLSupport.cginc` and `webgpu` warnings from Unturned SDK Nightfall Survival Expansion v3.26.3.12.2 when building in Unity 6 or Unity 2021+.

## Erros corrigidos / Fixed errors

```
Assets\Runtime\Assembly-CSharp\Unturned\Bundles\Assets.cs(2594,5): warning CS0162: Unreachable code detected
Shader error in 'Unturned/Screenshot': Couldn't open include file 'HLSLSupport.cginc'
Shader error in 'Standard/Lava': failed to open source file: 'HLSLSupport.cginc'
Shader warning in 'Hidden/PostProcessing/Uber': Unrecognized renderer for #pragma exclude_renderers: webgpu
... 50 errors
Error building Player: 50 errors
```

## Solução rápida / Quick fix

### 1. Script Python (recomendado)

```bash
python3 fix_all_v2.py /caminho/para/seu/projeto
# ou
python fix_all.py
```

Depois delete a pasta `Library` e abra o Unity novamente.

### 2. Dentro do Unity

1. Copie `HLSLSupport.cginc` para a raiz do projeto (ao lado de Assets)
2. Copie `Assets/Editor/UnturnedShaderFixer.cs` para `Assets/Editor/`
3. No Unity: `Unturned -> Fix All Shaders (Unity 6 Compatibility)`
4. Delete `Library` e reabra

### 3. Manual

Veja `README_FIX_PTBR.md` para instruções detalhadas em português.

## Arquivos / Files

- `HLSLSupport.cginc` - Shim de compatibilidade Unity 6
- `Assets/CGIncludes/HLSLSupport.cginc` - Cópia
- `Assets/Editor/UnturnedShaderFixer.cs` - Editor script auto-fix
- `fix_all.py` / `fix_all_v2.py` - Scripts Python que corrigem tudo automaticamente
- `Fixes/Shaders/` - Shaders corrigidos da versão mais recente do U3-SDK
- `README_FIX_PTBR.md` - Guia completo PT-BR
- `CORRIGIR.bat` / `CORRIGIR.sh` - Scripts para Windows/Linux

## Por que acontece?

- `HLSLSupport.cginc` foi removido no Unity 2021.2+. Projetos antigos que fazem `#include "HLSLSupport.cginc"` quebram.
- `webgpu` é renderer novo do Unity 6, pacote PostProcessing antigo não reconhece.
- `CS0162` é só warning, não quebra build. Corrigido com `#pragma warning disable 0162`.

## Unity recomendado

- Unturned SDK oficial recomenda **Unity 2021.3.45f1 LTS**
- Se usar Unity 6 (6000.x), aplique este fix
- Config: Color Space Linear, Built-in RP

## Testado

Fix testado e funcionando - remove todos os 50 erros de shader.

## Autor do fix

Gerado automaticamente para corrigir o release `U3-SDK-Nightfall-Survival-Expansion-v3.26.3.12.2.zip`

---

### Como aplicar no seu projeto original

1. Baixe este repositório ou copie os arquivos `HLSLSupport.cginc` e `fix_all_v2.py`
2. Coloque na raiz do seu projeto Unturned (onde tem a pasta Assets)
3. Rode `python3 fix_all_v2.py`
4. Delete `Library`
5. Abra Unity e faça Build - deve funcionar!

Se ainda der erro, abra `README_FIX_PTBR.md` para soluções avançadas.
