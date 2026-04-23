# Plano de Implementação - ManipuladorFotos

## 1. Objetivo
Construir uma interface moderna para gerenciamento de imagens e vídeos, com foco em busca, visualização lateral, identificação de duplicidades/similaridades e ações em massa seguras.

## 2. Escopo Funcional

### 2.1 Interface (UI)
- Tela principal moderna com layout em 3 áreas:
  - Barra superior de filtros e ações.
  - Lista central de arquivos.
  - Painel lateral de pré-visualização do item selecionado.
- Lista deve exibir: `Nome`, `Extensão`, `Data`, `Tamanho`.
- Visualização lateral da imagem/vídeo ao selecionar um item.
- Suporte responsivo para diferentes tamanhos de janela.

### 2.2 Abas da aplicação
- Aba `Organizar Fotos`:
  - Busca, filtros e análise de duplicadas/parecidas.
- Aba `Arquivos Indesejados`:
  - Selecionar tipos de arquivo para limpeza em pasta atual ou subpastas.
  - Exemplo: `.tmp`, `.db`, `.ini`, `.log`, `.heic` (quando não desejado), outras extensões escolhidas pelo usuário.

### 2.3 Filtros e Busca
- Busca por parte do nome (`contains`, sem diferenciar maiúsculas/minúsculas).
- Filtro por intervalo de `Data de criação`.
- Filtro por intervalo de `Data de modificação`.
- Filtro por intervalo de tamanho de arquivo (KB/MB/GB).
- Filtro por tipo/extensão (ex: `.jpg`, `.png`, `.webp`, `.mp4`).
- Opção para aplicar em:
  - Pasta atual.
  - Pasta atual + subpastas.

### 2.4 Regras de detecção para limpeza
- Duplicadas por mesmo conteúdo (hash exato), mesmo com nomes diferentes.
- Duplicadas por `mesmo nome` (regra opcional).
- Duplicadas por `mesmo tamanho` (regra opcional, com alerta de baixa precisão).
- Fotos semelhantes tiradas em sequência:
  - Similaridade visual por hash perceptual.
  - Diferença de tempo em segundos configurável (ex: 2s, 5s, 10s, 30s).

### 2.5 Fluxo obrigatório antes de excluir
- Antes de qualquer exclusão, o usuário deve receber uma `Lista de Exclusão` com:
  - Thumbnail/preview.
  - Nome, extensão, tamanho, data, caminho.
  - Motivo da sugestão (mesmo hash, mesmo nome, mesmo tamanho, semelhante em X segundos).
- O usuário pode:
  - Marcar/desmarcar itens individualmente.
  - Manter automaticamente a "melhor foto" do grupo (maior resolução ou arquivo mais recente).
  - Confirmar exclusão apenas após revisão.

### 2.6 Problemas a resolver
- Detectar fotos duplicadas com nomes diferentes.
- Detectar fotos parecidas tiradas em sequência.
- Exclusão em massa de fotos duplicadas/parecidas com revisão prévia.
- Exclusão em massa de extensões não desejadas na pasta/subpastas.
- Separação automática em massa de fotos e vídeos.

## 3. Arquitetura Técnica Proposta

### 3.1 Padrão de projeto
- Usar `MVVM` para separar interface e regras.
- Camadas sugeridas:
  - `Views` (XAML)
  - `ViewModels`
  - `Services` (scan, hash, similaridade, operações de arquivo)
  - `Models` (metadados e resultados de análise)

### 3.2 Modelos principais
- `MediaItem`
  - Caminho completo, Nome, Extensão, Tamanho, DataCriação, DataModificação, Tipo (Foto/Vídeo/Outro), Resolução.
  - Hash exato (opcional lazy)
  - Hash perceptual (opcional lazy)
- `DuplicateGroup`
  - Tipo de regra (`Hash`, `Nome`, `Tamanho`) + itens do grupo.
- `SimilarGroup`
  - Grupo de itens similares + score/confiança + diferença de tempo.
- `DeletionCandidate`
  - Item + motivo + ação sugerida (`Manter`/`Excluir`).

### 3.3 Serviços principais
- `FileScannerService`
  - Varredura da pasta com/sem subpastas.
  - Coleta de metadados.
- `FilterService`
  - Aplica filtros combinados.
- `DuplicateDetectorService`
  - Hash exato (SHA-256) para duplicados reais.
  - Agrupamento opcional por mesmo nome e mesmo tamanho.
- `SimilarityDetectorService`
  - pHash/aHash/dHash para similaridade visual.
  - Janela temporal em segundos para sequência.
- `ReviewListService`
  - Monta a lista de exclusão com motivo detalhado.
  - Sugere automaticamente o melhor arquivo para manter por grupo.
- `BatchOperationService`
  - Exclusão em massa (preferencialmente enviando para lixeira).
  - Movimentação em massa (separar fotos/vídeos).
  - Limpeza por extensões indesejadas.

## 4. Estratégia de Implementação (Fases)

### Fase 1 - Base da UI e navegação de pasta
- Criar layout moderno (toolbar + grid/lista + painel lateral).
- Adicionar abas `Organizar Fotos` e `Arquivos Indesejados`.
- Adicionar seleção de pasta e opção `Incluir subpastas`.
- Carregar lista de arquivos com colunas solicitadas.
- Exibir pré-visualização lateral ao selecionar item.

### Fase 2 - Filtros completos
- Implementar busca por nome.
- Implementar filtro de criação/modificação por intervalo.
- Implementar filtro de tamanho por intervalo.
- Implementar filtro por extensão/tipo.
- Atualização em tempo real da lista filtrada.

### Fase 3 - Duplicados e semelhantes
- Duplicados reais por hash exato.
- Duplicados opcionais por mesmo nome e mesmo tamanho.
- Similaridade visual por hash perceptual + threshold configurável.
- Regra de sequência por diferença de segundos configurável.
- Exibir agrupamento e contagem de itens afetados.

### Fase 4 - Lista de exclusão e ações seguras
- Gerar `Lista de Exclusão` antes da ação final.
- Permitir revisar, desmarcar e comparar visualmente.
- Exibir motivo da exclusão para cada item.
- Exclusão em massa com confirmação final e log.
- Separar automaticamente:
  - Fotos para `Fotos/`
  - Vídeos para `Videos/`

### Fase 5 - Aba de arquivos indesejados
- Seleção rápida de extensões comuns para limpeza.
- Campo para adicionar extensões personalizadas.
- Pré-lista de arquivos encontrados (pasta/subpastas).
- Exclusão em massa com `Dry Run` + confirmação.

### Fase 6 - Robustez e performance
- Processamento assíncrono com cancelamento.
- Barra de progresso em scans e análises.
- Cache de hashes para acelerar reanálises.
- Tratamento de erros (arquivo bloqueado, permissão, caminho inválido).

## 5. Segurança das Operações
- Ações destrutivas com dupla confirmação.
- Preferir mover para lixeira em vez de apagar permanentemente.
- Criar log de ações em arquivo (`logs/operations-YYYY-MM-DD.log`).
- Botão de simulação (`Dry Run`) para validar antes de executar.
- Botão de exportar lista antes da exclusão (`CSV`/`TXT`).

## 6. Sugestões para limpar rápido sem perder fotos importantes
- Modo `Conservador` (padrão): exclui apenas duplicadas por hash exato.
- Modo `Revisão Guiada`: para semelhantes, sempre manter 1 foto por sequência e revisar o resto.
- Prioridade automática para manter:
  - Maior resolução.
  - Maior tamanho útil (evita miniaturas/compressões ruins).
  - Data mais recente (ou mais antiga, escolha configurável).
- Etiqueta `Favoritas/Proteger` para impedir exclusão de arquivos marcados.
- Atalho de produtividade: selecionar um grupo inteiro e "manter somente a melhor" com 1 clique.
- Mostrar economia estimada de espaço antes de confirmar exclusão.

## 7. Critérios de Aceite
- Usuário consegue listar arquivos e ver preview lateral.
- Todos os filtros solicitados funcionam em conjunto.
- Duplicados com nomes diferentes são detectados com precisão.
- Duplicação por nome e tamanho funciona como regra opcional.
- Semelhantes em sequência por segundos são agrupadas com score visível.
- Lista de exclusão é exibida antes da remoção e permite revisão item a item.
- Aba `Arquivos Indesejados` remove extensões escolhidas em pasta/subpastas.
- Separação fotos/vídeos funciona na pasta atual e em subpastas.

## 8. Backlog Técnico Inicial
1. Estruturar pastas `Models`, `ViewModels`, `Services`.
2. Criar `MediaItem`, `DeletionCandidate` e `MainViewModel`.
3. Implementar `FileScannerService` com recursão opcional.
4. Montar UI com abas, grid e preview lateral.
5. Implementar filtros básicos.
6. Implementar duplicados por hash, nome e tamanho.
7. Implementar similaridade por hash perceptual + segundos.
8. Implementar tela/lista de revisão de exclusão.
9. Implementar aba de arquivos indesejados.
10. Implementar operações em massa com `Dry Run`, logs e lixeira.
11. Criar testes unitários de filtros e detecção.

## 9. Próximo Passo Recomendado
Começar pela **Fase 1**, entregando interface com abas, listagem e preview lateral. Em seguida implementar o fluxo de `Lista de Exclusão` para validar segurança antes das exclusões em massa.
