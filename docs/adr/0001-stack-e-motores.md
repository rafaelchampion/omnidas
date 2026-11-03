# ADR 0001: Stack e motores de conversão

## Status
Aceito

## Decisão
- C# / .NET 9; GUI em WinUI 3; núcleo `Omnidas.Core` compartilhado por CLI, GUI e menu de contexto.
- Conversão feita por motores open-source embarcados (FFmpeg, libvips/ImageMagick, Pandoc, 7-Zip) atrás de `IEngineAdapter`. Motores pesados (LibreOffice, Whisper, Calibre, OCR, 3D) são Pacotes Extras baixados sob demanda, verificados por hash/assinatura.
- Menu de contexto: item único "Converter com Omnidas…" que abre uma mini-janela. `IExplorerCommand` empacotado em MSIX esparso (Win11) e chaves de registro (menu clássico/Win10).
- Licença GPL-3.0, repositório público. Sem telemetria.

## Consequências
- Criação de `.rar` não é suportada (licença); apenas leitura.
- A mini-janela precisa abrir em menos de ~300 ms (spike no M1).
