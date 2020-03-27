- O conceito de monitor define um meta-sincronizador adequado à implementação de sincronizadores (ou schedulers de "recursos").

- Unifica todos os aspectos envolvidos na implementação de sincronizadores: os dados partilhados, o código que acede a esses dados, o acesso aos dados partilhados em exclusão mútua e a possibilidade de bloquear e desbloquear threads em coordenação com a exclusão mútua.
- Este mecanismo foi proposto como construção de uma linguagem de alto nível (Concurrent Pascal) semelhante à definição de classe nas linguagens orientadas
por objectos.
- Eram considerados dois tipos de procedimentos: os procedimentos de entrada
(públicos),que podem ser invocados de fora do monitor e os procedimentos internos (privados) que apenas podem ser invocados pelos procedimentos de entrada.
- O monitor garante, que num determinado momento, apenas uma thread está
 *dentro* 

- Para bloquear as threads dentro 

- Semântica de Brinch Hansen e Hoare

	- A semântica da sinalização das threads bloqueados no monitor garante que
	uma thread sinalizada tem a garantia de que o estado dos dados partilhados
	mutáveis era o estado imediatamente antes da chamada cvondition-varible.signal
	se encontram 
