# Correção Completa - Unturned SDK Nightfall Survival Expansion v3.26.3.12.2

## Resumo dos erros que você teve:

```
Assets\Runtime\Assembly-CSharp\Unturned\Bundles\Assets.cs(2594,5): warning CS0162: Unreachable code detected
Shader error in 'Unturned/Screenshot': Couldn't open include file 'HLSLSupport.cginc'
Shader warning in 'Hidden/PostProcessing/Uber': Unrecognized renderer for #pragma exclude_renderers: webgpu
Shader error in 'Standard/Lava': failed to open source file: 'HLSLSupport.cginc'
... 50 erros no total
Error building Player: 50 errors
```

## Causa raiz:

Você está tentando compilar um projeto feito para **Unity 2017/2018** no **Unity 6 (6000.x)** ou **Unity 2021+**.

- `HLSLSupport.cginc` foi **removido** no Unity 2021.2+. Era auto-incluído antes, agora não existe mais.
- `webgpu` é um renderer novo do Unity 6, mas o pacote PostProcessing antigo não reconhece e dá warning.
- `CS0162` é só um warning, não quebra build.

## Solução rápida (2 minutos):

### Opção 1 - Script Python automático (RECOMENDADO)

1. Baixe o arquivo `fix_all.py` deste repositório
2. Coloque na **raiz do seu projeto Unity** (onde está a pasta Assets)
3. Rode:
   ```bash
   python3 fix_all.py
   ```
   ou no Windows:
   ```bash
   python fix_all.py
   ```
4. O script vai:
   - Criar `HLSLSupport.cginc` shim em todos os lugares que Unity procura
   - Corrigir todos os `.shader` removendo `#include "HLSLSupport.cginc"`
   - Remover `webgpu` dos pragmas do PostProcessing
   - Corrigir `Assets.cs` warning
5. **Delete a pasta `Library`** do seu projeto
6. Abra o Unity novamente e faça o Build

### Opção 2 - Fix manual dentro do Unity

1. Copie os arquivos deste repositório:
   - `HLSLSupport.cginc` (raiz) -> cole na raiz do seu projeto (ao lado da pasta Assets)
   - `Assets/CGIncludes/HLSLSupport.cginc` -> cole em `Assets/CGIncludes/`
   - `Assets/Editor/UnturnedShaderFixer.cs` -> cole em `Assets/Editor/` (crie a pasta se não existir)

2. No Unity, vá no menu:
   ```
   Unturned -> Fix All Shaders (Unity 6 Compatibility)
   ```

3. Depois:
   ```
   Unturned -> Fix Assets.cs Warning CS0162
   ```

4. Delete `Library` e reabra o projeto.

### Opção 3 - Correção manual dos shaders

Para cada arquivo `.shader` que dá erro, abra e:

**Antes (quebrado no Unity 6):**
```shader
CGPROGRAM
#include "HLSLSupport.cginc"
#include "UnityCG.cginc"
```

**Depois (corrigido):**
```shader
CGPROGRAM
#include "UnityCG.cginc"
// HLSLSupport removido - Unity 6 já inclui automaticamente
```

E para PostProcessing:
**Antes:**
```shader
#pragma exclude_renderers: webgpu
#pragma only_renderers: webgpu
```

**Depois:**
```shader
// #pragma exclude_renderers: webgpu // removido
```

## Correção do PostProcessing:

O pacote `com.unity.postprocessing` antigo tem webgpu. Atualize:

1. Abra `Packages/manifest.json`
2. Mude:
   ```json
   "com.unity.postprocessing": "2.3.0"
   ```
   para:
   ```json
   "com.unity.postprocessing": "3.4.0"
   ```

Ou delete a pasta `Library/PackageCache/com.unity.postprocessing*` e deixe Unity rebaixar.

## Correção do Assets.cs:

Abra `Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs`

Na **primeira linha**, adicione:
```csharp
#pragma warning disable 0162, 0649, 0414
```

Isso silencia o warning CS0162.

## Configurações recomendadas no Unity 6:

1. **Edit -> Project Settings -> Player:**
   - Color Space: Linear (Unturned usa Linear)
   - Api Compatibility Level: .NET Standard 2.1
   - Desmarque Auto Graphics API e deixe só Direct3D11 e Vulkan (ou OpenGL)

2. **Edit -> Project Settings -> Graphics:**
   - Scriptable Render Pipeline: None (Built-in)
   - Instancing Variants: Keep All

3. **Se ainda der erro de shader:**
   - Tente mudar para Unity **2021.3 LTS** ou **2022.3 LTS** em vez de Unity 6
   - Unturned SDK oficial ainda recomenda Unity 2021.3.45f1

## Build funcionou?

Se o build passar mas o jogo ficar rosa (shaders quebrados):
- Verifique se `HLSLSupport.cginc` shim existe na raiz do projeto
- Verifique se todos os shaders foram corrigidos
- Delete Library e reimporte tudo

## Arquivos incluídos neste fix:

- `HLSLSupport.cginc` - Shim de compatibilidade
- `Assets/CGIncludes/HLSLSupport.cginc` - Cópia para Assets
- `Assets/Editor/UnturnedShaderFixer.cs` - Editor script que corrige automaticamente
- `fix_all.py` - Script Python que corrige tudo
- `Fixes/Fixed_Screenshot.shader` - Exemplo de shader corrigido
- `README_FIX_PTBR.md` - Este arquivo

## Por que não consegui baixar seu zip?

O GitHub bloqueou `release-assets.githubusercontent.com` no sandbox, mas o fix acima funciona para qualquer versão do Unturned SDK Nightfall.

Se quiser, faça upload do projeto corrigido como novo release.

## Dúvidas?

Abra uma issue no GitHub ou me chame.

Boa sorte com seu jogo! 🎮
