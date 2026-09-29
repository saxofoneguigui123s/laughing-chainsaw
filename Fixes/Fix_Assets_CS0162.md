# Fix para warning CS0162 em Assets.cs

O erro:
```
Assets\Runtime\Assembly-CSharp\Unturned\Bundles\Assets.cs(2594,5): warning CS0162: Unreachable code detected
```

Causa:
No arquivo Assets.cs existe um bloco:

```csharp
#if !DEDICATED_SERVER && !UNITY_EDITOR && !DEVELOPMENT_BUILD
    CommandWindow.LogError("Hosting dedicated servers using client files has been deprecated since June 2019.");
    ...
    return; // Abort startup.
#endif
```

Quando você compila no editor ou como client, o código após o #endif pode ser considerado unreachable em algumas configurações.

Solução 1 - Adicionar pragma no topo do arquivo (recomendado):
```csharp
#pragma warning disable 0162, 0649, 0414
```

Solução 2 - Editar o arquivo:
Abra `Assets/Runtime/Assembly-CSharp/Unturned/Bundles/Assets.cs`
Na linha 1, adicione:
```csharp
#pragma warning disable CS0162
```

Solução 3 - Usar o script fix_all.py que faz automaticamente.

Este warning NÃO quebra o build, é apenas um aviso. O que quebra o build são os 50 erros de shader.
