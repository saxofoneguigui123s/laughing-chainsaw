@echo off
echo Corrigindo Unturned SDK para Unity 6...
echo.

REM Criar HLSLSupport.cginc na raiz
echo Criando HLSLSupport.cginc shim...
echo // HLSLSupport shim > HLSLSupport.cginc
echo #ifndef HLSLSUPPORT_INCLUDED >> HLSLSupport.cginc
echo #define HLSLSUPPORT_INCLUDED >> HLSLSupport.cginc
echo #endif >> HLSLSupport.cginc

REM Criar nas outras pastas
if not exist "Assets\CGIncludes" mkdir "Assets\CGIncludes"
copy /Y HLSLSupport.cginc "Assets\CGIncludes\HLSLSupport.cginc"
copy /Y HLSLSupport.cginc "Assets\HLSLSupport.cginc"
if not exist "Assets\Game\Sources\Shaders" mkdir "Assets\Game\Sources\Shaders"
copy /Y HLSLSupport.cginc "Assets\Game\Sources\Shaders\HLSLSupport.cginc"

echo.
echo Rodando fix_all.py se Python estiver instalado...
python fix_all.py
if %errorlevel% neq 0 (
    python3 fix_all.py
)

echo.
echo Correcao concluida!
echo Agora DELETE a pasta Library e abra o Unity novamente.
echo.
pause
