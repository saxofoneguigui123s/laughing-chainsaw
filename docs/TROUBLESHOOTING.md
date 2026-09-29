# Correção do build U3-SDK: `HLSLSupport.cginc`

## Diagnóstico

O bloco de erros abaixo tem **uma causa raiz**:

```text
Shader error: Couldn't open include file 'HLSLSupport.cginc'
Shader error: failed to open source file: 'HLSLSupport.cginc'
Error building Player: 50 errors
```

`HLSLSupport.cginc` é um include embutido do **editor Unity**, não é um shader do Nightfall e não deve ser copiado para `Assets/`. Quando ele não existe ou o editor errado abre o projeto, todos os shaders que dependem da biblioteca padrão falham em sequência: Screenshot, Standard, Landscape, Post Processing, Intersect e outros. Portanto, editar cada shader não corrige o problema.

A tag U3-SDK `v3.26.3.12` exige exatamente **Unity 2022.3.62f3**. Ela fixa `com.unity.postprocessing` em `3.4.0`.

## Correção no Windows

1. Feche Unity e Unity Hub para esse projeto.
2. No Unity Hub, instale **Unity 2022.3.62f3**. Não use Unity 6/6000, 2023, outra LTS ou uma instalação parcialmente copiada.
3. Confirme que o arquivo existe na instalação do editor. Ele estará em um destes locais:
   - `…\Editor\Data\Resources\CGIncludes\HLSLSupport.cginc`
   - `…\Editor\Data\CGIncludes\HLSLSupport.cginc`
4. Se não existir, remova a instalação 2022.3.62f3 no Hub e instale-a novamente. Não baixe um `.cginc` aleatório nem crie um arquivo vazio.
5. No diretório que contém `Assets`, `Packages` e `ProjectSettings`, execute:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\tools\Repair-U3SdkEnvironment.ps1 `
     -ProjectPath "C:\caminho\para\U3-SDK" `
     -CleanProjectCache `
     -OpenProject
   ```

   Se o Unity estiver instalado em outro local, acrescente por exemplo:

   ```powershell
   -UnityPath "D:\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
   ```

6. Aguarde a reimportação da pasta `Library` terminar. Só então abra `Assets/GameStartup.unity` e faça o build novamente.

O script apenas valida versão/editor/pacotes e remove caches regeneráveis (`Library`, `Temp`, `obj`) quando `-CleanProjectCache` é informado. Ele **não** remove `Assets`, `Packages` ou `ProjectSettings`.

## Avisos que não quebram o build

- `CS0162` em `Assets.cs(2594,5)` é um aviso do código de inicialização do SDK sob determinadas flags de compilação. Não é a causa da falha de 50 shaders.
- `Unrecognized renderer ... webgpu` vem do pacote Post Processing. Em Unity 2022.3 ele é um aviso de pragma, não a falha do Player build.
- `shader is not supported on this GPU` e `Both vertex and fragment programs...` são efeitos em cascata depois que o include base não pôde ser lido.

Se os erros continuarem **depois** do script validar o `HLSLSupport.cginc`, envie `Editor.log`, `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` e `Packages/packages-lock.json`. Não altere os shaders de Unturned para tentar mascarar o include ausente.
