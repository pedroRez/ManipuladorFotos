# ManipuladorFotos

Aplicação desktop (WPF/.NET 8) para organizar fotos e vídeos com foco em limpeza segura: busca por filtros, identificação de duplicadas/semelhantes, revisão visual antes da exclusão e remoção de arquivos indesejados.

## Funcionalidades

- Interface moderna com abas:
  - `Organizar Fotos`
  - `Arquivos Indesejados`
  - `Lista de Exclusão`
- Busca e filtros por:
  - nome
  - extensão
  - tipo (foto, vídeo, outro)
  - data de criação/modificação
  - tamanho
- Pré-visualização lateral da mídia selecionada.
- Detecção de duplicadas e semelhantes:
  - hash exato (mesmo conteúdo)
  - mesmo nome (opcional)
  - mesmo tamanho (opcional)
  - semelhantes por sequência de segundos + distância de similaridade (opcional)
- Proteção de segurança na exclusão:
  - lista de revisão antes de apagar
  - foto original protegida em cada grupo (não pode ser excluída)
  - confirmação com quantidade e espaço estimado
  - exclusão para lixeira
- Limpeza por extensões indesejadas em pasta atual ou subpastas.

## Tecnologias

- .NET 8 (`net8.0-windows`)
- WPF
- Arquitetura baseada em MVVM (ViewModels + Services + Models)

## Estrutura

- `ManipuladorFotos/MainWindow.xaml`: layout e abas da interface.
- `ManipuladorFotos/ViewModels/MainViewModel.cs`: estado da UI e comandos principais.
- `ManipuladorFotos/Services/`
  - `FileScannerService.cs`: varredura de arquivos e metadados.
  - `FilterService.cs`: aplicação de filtros.
  - `DuplicateAnalysisService.cs`: análise de duplicadas/semelhantes.
- `ManipuladorFotos/Models/`
  - `MediaItem.cs`: metadados de mídia.
  - `DeletionCandidate.cs`: itens de revisão de exclusão.

## Como executar

### Requisitos

- Windows
- SDK .NET 8 instalado

### Passos

1. Abra a pasta do repositório.
2. Execute:

```powershell
dotnet build
```

3. Rode o app:

```powershell
dotnet run --project .\ManipuladorFotos\ManipuladorFotos.csproj
```

## Fluxo recomendado de uso

1. Em `Organizar Fotos`, selecione pasta e escaneie.
2. Ajuste filtros para localizar o conjunto desejado.
3. Em `Lista de Exclusão`, configure regras e gere a lista.
4. Revise visualmente os grupos:
   - itens `Candidato` podem ser excluídos
   - item `Original protegida` fica bloqueado para evitar perda total
5. Exclua os marcados (lixeira) após confirmação.
6. Em `Arquivos Indesejados`, limpe extensões específicas.

## Segurança e comportamento importante

- A lista é limpa ao iniciar nova busca para evitar resultados antigos misturados.
- A original protegida não pode ser marcada para exclusão.
- Se o usuário tentar marcar a original, o sistema mostra aviso e desfaz a marcação.

## Status

Projeto em evolução incremental com foco em UX de revisão e segurança das operações em massa.
