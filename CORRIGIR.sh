#!/bin/bash
echo "Corrigindo Unturned SDK para Unity 6..."

# Criar shim
cat > HLSLSupport.cginc << 'EOF'
// HLSLSupport shim for Unity 6
#ifndef HLSLSUPPORT_INCLUDED
#define HLSLSUPPORT_INCLUDED
#endif
EOF

mkdir -p Assets/CGIncludes
cp HLSLSupport.cginc Assets/CGIncludes/HLSLSupport.cginc
cp HLSLSupport.cginc Assets/HLSLSupport.cginc
mkdir -p Assets/Game/Sources/Shaders
cp HLSLSupport.cginc Assets/Game/Sources/Shaders/HLSLSupport.cginc

echo "Rodando fix_all.py..."
python3 fix_all.py || python fix_all.py

echo "Correcao concluida! Delete a pasta Library e abra o Unity novamente."
