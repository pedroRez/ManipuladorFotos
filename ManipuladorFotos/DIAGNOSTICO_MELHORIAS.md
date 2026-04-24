# Diagnóstico de Melhorias - ManipuladorFotos

Data: 24/04/2026  
Status: pronto para execução

## Objetivo
Melhorar desempenho, estabilidade e clareza para usuário leigo, com foco em:
- Scan mais rápido em grandes volumes
- Menos travamentos/fechamentos inesperados
- Cancelamento realmente responsivo
- Fluxo mais previsível (sem retrabalho e sem scans redundantes)

## Prioridades (ordem de impacto)

## 1) Scan em 2 fases (maior ganho de velocidade)
Problema atual:
- O scan já lê metadados pesados de imagem durante a varredura geral.
- Em pastas grandes, isso aumenta muito o tempo total.

Melhoria:
- Fase 1 (rápida): coletar apenas `FileInfo` (nome, extensão, tamanho, datas, tipo).
- Fase 2 (sob demanda): carregar metadados pesados (resolução/EXIF) somente quando necessário:
  - item selecionado para preview
  - geração de lista de exclusão, se a regra realmente exigir

Arquivos principais:
- `ManipuladorFotos/Services/FileScannerService.cs`
- `ManipuladorFotos/ViewModels/MainViewModel.cs`

Critério de aceite:
- Tempo do scan inicial reduzido de forma perceptível em pastas grandes.
- UI mostra resultados iniciais mais rápido, sem perda funcional.

## 2) Evitar dupla varredura percebida
Problema atual:
- Em alguns fluxos, há scan + snapshot de consistência da pasta, parecendo scan duplicado.

Melhoria:
- Reutilizar resultado do último scan válido na sessão.
- Invalidar cache apenas quando necessário (pasta, subpastas, contagem, timestamp ou mudança explícita).
- Exibir status claro quando estiver reaproveitando scan anterior.

Arquivos principais:
- `ManipuladorFotos/ViewModels/MainViewModel.cs`

Critério de aceite:
- Geração de lista de exclusão não dispara nova varredura quando não houve alteração real na pasta.

## 3) Cancelamento mais responsivo em massa
Problema atual:
- Em exclusão/movimentação em paralelo, cancelamento pode demorar para refletir.

Melhoria:
- Ajustar paralelismo para I/O (`File.Move`) de forma conservadora.
- Checar cancelamento com maior frequência.
- Mensagem de status separando:
  - "Cancelando..."
  - "Finalizando itens em andamento..."
  - "Cancelado"

Arquivos principais:
- `ManipuladorFotos/ViewModels/MainViewModel.cs`

Critério de aceite:
- Ao cancelar operação grande, resposta visual imediata e término em tempo menor.

## 4) Estabilidade com listas grandes (8k+)
Problema atual:
- Fechamento inesperado da aplicação em cenários de revisão extensa.
- Falta proteção global para exceções não tratadas.

Melhoria:
- Adicionar tratamento global:
  - `Application.DispatcherUnhandledException`
  - `AppDomain.CurrentDomain.UnhandledException`
  - `TaskScheduler.UnobservedTaskException`
- Em caso de erro: manter app aberto, notificar usuário e preservar estado possível.

Arquivos principais:
- `ManipuladorFotos/App.xaml.cs`

Critério de aceite:
- Erro não tratado não derruba a aplicação silenciosamente na maior parte dos cenários.

## 5) Correções de consistência funcional
Problema atual identificado:
- Filtro de data de modificação está comparando campo de data incorreto.

Melhoria:
- `ModifiedFrom`/`ModifiedTo` devem usar `LastWriteTime` (e não `PrimaryPhotoDate`).

Arquivos principais:
- `ManipuladorFotos/Services/FilterService.cs`

Critério de aceite:
- Resultado filtrado por modificação corresponde à data de modificação real do arquivo.

## Backlog de execução sugerido
1. Corrigir filtro de modificação (`FilterService`) - baixo risco, ganho imediato de confiança.
2. Adicionar hardening global de exceções (`App.xaml.cs`) - reduz fechamento inesperado.
3. Implementar scan em 2 fases (`FileScannerService` + ajustes no `MainViewModel`).
4. Otimizar lógica de reaproveitamento de scan e consistência de snapshot.
5. Ajustar cancelamento e mensagens de progresso nas operações em massa.
6. Rodar validação com pasta de teste grande e revisão focada.

## Métricas de validação (recomendadas)
- Tempo de scan inicial (antes/depois) na mesma pasta.
- Tempo de geração da lista de exclusão (antes/depois).
- Tempo entre clique em cancelar e estado final de cancelado.
- Número de encerramentos inesperados durante revisão de 8.000+ itens.

## Riscos e cuidados
- Não alterar comportamento de proteção da "Original protegida".
- Manter compatibilidade com fluxo de desfazer (undo batch).
- Evitar regressão no modo revisão focada.

## Definição de pronto
- Sem regressão funcional nos 3 fluxos:
  - Organizar
  - Arquivos indesejados
  - Lista de exclusão
- Cancelamento perceptivelmente melhor.
- Scan inicial mais rápido.
- Filtro de modificação corrigido.
