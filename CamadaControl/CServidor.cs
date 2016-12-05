using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CamadaControl
{
    //DELEGATE GLOBAL - Este delegate é necessário para especificar os parametros que estamos passando com o nosso evento
    public delegate void StatusChangedEventHandler(object escritor, StatusChangedEventArgs e);


    //CLASSE 1

    public class StatusChangedEventArgs : EventArgs // Trata os argumentos para o evento StatusChanged
    {
        //VARIÁVEIS

        private string _mensagemEvento; // Estamos interessados na mensagem descrevendo o evento

        public string MensagemEvento    // Propriedade para retornar e definir um mensagem do evento
        {
            get { return _mensagemEvento; }
            set { _mensagemEvento = value; }
        }

        //CONSTRUTOR

        public StatusChangedEventArgs(string msgEvento) // Define a mensagem do evento
        {
            MensagemEvento = msgEvento;
        }
    }


    //CLASSE 2

    public class CServidor
    {
        //VARIÁVEIS

        private static Hashtable _tabelaUsuarios = new Hashtable(5); // Tabela que armazena os usuários (acessado/consultado por usuário)
        private static Hashtable _tabelaConexoes = new Hashtable(5); // Tabela que armazena as conexões (acessada/consultada por conexão)
        private IPAddress _enderecoIP;      // Armazena o endereço IP passado        
        private TcpClient _tcpCliente;
        private Thread _threadListener;     // A thread que ira tratar o escutador de conexões
        private TcpListener _tcpLstCliente; // O objeto TCP object que escuta as conexões
        bool _servRodando = false;          // Ira dizer ao laço while para manter a monitoração das conexões
        private static readonly Random _rnd = new Random();
        private static Dictionary<String, char> _tabuleiro = new Dictionary<String, char>(), _letrasSelecionadas = new Dictionary<String, char>();
        private static Dictionary<String, bool> _letrasClicadas = new Dictionary<String, bool>();
        private static int _viradasPermitidas = 0;
        //private bool _isTurno;
        private static bool _partidaEmAndamento = false;

        public static Hashtable TabelaUsuarios
        {
            get { return CServidor._tabelaUsuarios; }
            set { CServidor._tabelaUsuarios = value; }
        }

        public static Hashtable TabelaConexoes
        {
            get { return CServidor._tabelaConexoes; }
            set { CServidor._tabelaConexoes = value; }
        }

        public IPAddress EnderecoIP
        {
            get { return _enderecoIP; }
            set { _enderecoIP = value; }
        }

        public TcpClient TcpCliente
        {
            get { return _tcpCliente; }
            set { _tcpCliente = value; }
        }

        public Thread ThreadListener
        {
            get { return _threadListener; }
            set { _threadListener = value; }
        }

        public TcpListener TcpLstCliente
        {
            get { return _tcpLstCliente; }
            set { _tcpLstCliente = value; }
        }

        public bool ServRodando
        {
            get { return _servRodando; }
            set { _servRodando = value; }
        }

        public static Random Rnd
        {
            get { return CServidor._rnd; }
        }

        public static Dictionary<String, char> Tabuleiro
        {
            get { return CServidor._tabuleiro; }
            set { CServidor._tabuleiro = value; }
        }

        public static Dictionary<String, char> LetrasSelecionadas
        {
            get { return CServidor._letrasSelecionadas; }
            set { CServidor._letrasSelecionadas = value; }
        }

        public static Dictionary<String, bool> LetrasClicadas
        {
            get { return CServidor._letrasClicadas; }
            set { CServidor._letrasClicadas = value; }
        }

        public static int ViradasPermitidas
        {
            get { return CServidor._viradasPermitidas; }
            set { CServidor._viradasPermitidas = value; }
        }

        public static bool PartidaEmAndamento
        {
            get { return CServidor._partidaEmAndamento; }
            set { CServidor._partidaEmAndamento = value; }
        }


        //DELEGATES - O evento e o seu argumento irá notificar o formulário quando um usuário se conecta, desconecta, envia uma mensagem,etc

        public static event StatusChangedEventHandler StatusChanged;
        private static StatusChangedEventArgs E;

        //CONSTRUTOR

        public CServidor(IPAddress endereco) // Define o endereço IP para aquele retornado pela instanciação do objeto
        {
            EnderecoIP = endereco;
        }

        //MÉTODOS

        public static void IncluirUsuario(TcpClient socketUsuario, string nomeUsuario) // Inclui o usuário nas duas tabelas: "TabelaUsuarios" e "TabelaConexoes"
        {
            TabelaUsuarios.Add(nomeUsuario, socketUsuario);                            // Primeiro inclui o nome e conexão numa tabela organizada por usuários
            TabelaConexoes.Add(socketUsuario, nomeUsuario);                            // Primeiro inclui o nome e conexão numa tabela organizada por conexões
            EnviarMensagemAdmin(TabelaConexoes[socketUsuario] + " entrou..");          // Informa a nova conexão para todos os usuário e para o formulário do servidor
        }

        public static void RemoverUsuario(TcpClient socketUsuario)                     // Remove o usuário das tabelas (hash tables)
        {
            if (TabelaConexoes[socketUsuario] != null)                                 // Se o usuário existir
            {
                EnviarMensagemAdmin(TabelaConexoes[socketUsuario] + " saiu...");       // Primeiro mostra a informação e informa os outros usuários sobre a conexão
                TabelaUsuarios.Remove(TabelaConexoes[socketUsuario]);                  // Removeo usuário da hash table
                TabelaConexoes.Remove(socketUsuario);
            }
        }

        public static void OnStatusChanged(StatusChangedEventArgs e) // Este evento é chamado quando queremos disparar o evento StatusChanged
        {
            StatusChangedEventHandler statusHandler = StatusChanged;
            if (statusHandler != null)
            {
                statusHandler(null, e); // invoca o delegate
            }
        }

        public static void EnviarMensagemAdmin(string mensagem)     // Envia mensagens administrativas
        {
            StreamWriter swEscritor;

            E = new StatusChangedEventArgs("Admin: " + mensagem);  // Exibe primeiro na aplicação
            OnStatusChanged(E);

            TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count]; // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
            TabelaUsuarios.Values.CopyTo(tcpClientes, 0);                  // Copia os objetos TcpClient no array
            for (int i = 0; i < tcpClientes.Length; i++)                   // Percorre a lista de clientes TCP
            {
                try // Tenta enviar uma mensagem para cada cliente
                {
                    if (mensagem.Trim() == "" || tcpClientes[i] == null) // Se a mensagem estiver em branco ou a conexão for nula, segue para a próxima iteração...
                    {
                        continue;
                    }
                    swEscritor = new StreamWriter(tcpClientes[i].GetStream());  // Envia a mensagem para o usuário atual no laço
                    swEscritor.WriteLine("02|Admin: " + mensagem);
                    swEscritor.Flush();
                    swEscritor = null;
                }
                catch // Se houver um problema, o usuário não existe, então remove-o
                {
                    RemoverUsuario(tcpClientes[i]);
                }
            }
        }

        // Envia mensagens de um usuário para todos os outros
        public static void EnviarMensagem(string origem, string mensagem) //O parâmetro mensagem vai ser tudo que vem após o primeiro pipe da mensagem superior (resposta do cliente)
        {
            StreamWriter swEscritor;

            E = new StatusChangedEventArgs(origem + " disse : " + mensagem); // Primeiro exibe a mensagem na aplicação
            OnStatusChanged(E);

            TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count];   // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
            TabelaUsuarios.Values.CopyTo(tcpClientes, 0);                    // Copia os objetos TcpClient no array
            for (int i = 0; i < tcpClientes.Length; i++)                     // Percorre a lista de clientes TCP
            {
                try // Tenta enviar uma mensagem para cada cliente
                {
                    if (tcpClientes[i] == null) // Se a conexão for nula, segue para a próxima iteração...
                    {
                        continue;
                    }
                    swEscritor = new StreamWriter(tcpClientes[i].GetStream());  // Envia a mensagem para o usuário atual no laço
                    swEscritor.WriteLine("02|" + origem + " disse: " + mensagem);
                    swEscritor.Flush();
                    swEscritor = null;
                }
                catch // Se houver um problema , o usuário não existe , então remove-o
                {
                    RemoverUsuario(tcpClientes[i]);
                }
            }
        }

        // Envia mensagens de um usuário para todos os outros
        public static void EnviarMensagemResultadoDados(string origem, string mensagem) //O parâmetro mensagem vai ser tudo que vem após o primeiro pipe da mensagem superior (resposta do cliente)
        {
            StreamWriter swEscritor;

            ViradasPermitidas = Convert.ToInt32(mensagem); // APÓS PIPE, VAI PEGAR POSICAO CLICADA DA FIGURA linha 1 e coluna 1 por exemplo ------------------

            E = new StatusChangedEventArgs(origem + " rolou " + ViradasPermitidas); // Primeiro exibe a mensagem na aplicação da posição clicada pelo cliente da vez
            OnStatusChanged(E);

            TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count];   // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
            TabelaUsuarios.Values.CopyTo(tcpClientes, 0);                    // Copia os objetos TcpClient no array
            for (int i = 0; i < tcpClientes.Length; i++)                     // Percorre a lista de clientes TCP
            {
                /*try // Tenta enviar uma mensagem para cada cliente
                {*/
                if (mensagem.Trim() == "" || tcpClientes[i] == null) // Se a mensagem estiver em branco ou a conexão for nula, segue para a próxima iteração...
                {
                    continue;
                }
                swEscritor = new StreamWriter(tcpClientes[i].GetStream());  // Envia a mensagem para o usuário atual no laço
                swEscritor.WriteLine("10|" + ViradasPermitidas);            // Enviou a palavra, que será tratado por um método interno do cliente
                swEscritor.Flush();
                swEscritor.WriteLine("03|Admin: " + origem + " rolou " + ViradasPermitidas); // Depois exibe a mensagem no log de um dos clientes selecionados, da posição clicada pelo jogador da vez
                swEscritor.Flush();
                swEscritor = null;
                /*}
                catch // Se houver um problema , o usuário não existe , então remove-o
                {
                    RemoverUsuario(tcpClientes[i]);
                }*/
            }
        }

        // Envia mensagens de um usuário para todos os outros
        public static void EnviarMensagemMovimento(string origem, string mensagemPos) //O parâmetro mensagem vai ser tudo que vem após o primeiro pipe da mensagem superior (resposta do cliente)
        {
            StreamWriter swEscritor;

            char letra = 'W'; //VirarLetra vai retornar a letra equivalente à posição clicada (indicado no parâmetro mensagem) virarLetra(mensagem)
            //-------------------------------------------------------------
            if (!LetrasClicadas[mensagemPos])
            {
                if (ViradasPermitidas > 0)
                {
                    --ViradasPermitidas;
                    LetrasClicadas[mensagemPos] = true;
                    LetrasSelecionadas.Add(mensagemPos, Tabuleiro[mensagemPos]);
                    letra = Tabuleiro[mensagemPos];
                }
                /*else
                {
                    letra = 'W';    //Região do tabuleiro com letra ainda oculta.
                }*/
            }
            /*else if (Tabuleiro.ContainsKey(mensagem))
            {
                letra = Tabuleiro[mensagem];
            }
            else
            {
                letra = 'Y';     //Região do tabuleiro com letra descartada.
            }*/
            //------------------------------------------------------------

            if (!letra.Equals('W'))
            {
                E = new StatusChangedEventArgs(origem + " clicou linha_" + mensagemPos[0].ToString() + " coluna_" + mensagemPos[1].ToString()); // Primeiro exibe a mensagem na aplicação da posição clicada pelo cliente da vez
                OnStatusChanged(E);

                TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count];   // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
                TabelaUsuarios.Values.CopyTo(tcpClientes, 0);                    // Copia os objetos TcpClient no array
                for (int i = 0; i < tcpClientes.Length; i++)                     // Percorre a lista de clientes TCP
                {
                    if (tcpClientes[i] == null) // Se a conexão for nula, segue para a próxima iteração...
                    {
                        continue;
                    }
                    swEscritor = new StreamWriter(tcpClientes[i].GetStream());   // Envia a mensagem para o usuário atual no laço
                    swEscritor.WriteLine("11|" + mensagemPos + "|" + letra); // Enviou a posição e a letra, que será tratado por um método interno do cliente
                    swEscritor.Flush();
                    swEscritor.WriteLine("03|Admin: " + origem + " clicou lin" + mensagemPos[0] + " col" + mensagemPos[1]); // Depois exibe a mensagem no log de todos os clientes da posição clicada pelo cliente da vez
                    swEscritor.Flush();
                    swEscritor = null;
                }
            }
        }

        // Envia mensagens de um usuário para todos os outros
        public static void EnviarMensagemPalavra(string origem, string mensagem) //O parâmetro mensagem vai ser tudo que vem após o primeiro pipe da mensagem superior (resposta do cliente)
        {
            StreamWriter swEscritor;

            //Se o jogador espertinho digitar uma letra que não foi exibida, ela é ignorada.
            string palavra = "";
            //palavra = adicionarPalavra(mensagem); //Trata a mensagem recebida, retirando letras que não foram selecionadas.
            //-------------------------------------------------------
            StringBuilder sb = new StringBuilder();
            foreach (char letraUsada in mensagem)
            {
                foreach (string chave in LetrasSelecionadas.Keys)
                {
                    if (letraUsada.Equals(LetrasSelecionadas[chave]))
                    {
                        sb.Append(letraUsada);
                        break;
                    }
                }
            }
            palavra = sb.ToString();
            //-------------------------------------------------------

            E = new StatusChangedEventArgs(origem + " inseriu a palavra " + palavra); // Primeiro exibe a mensagem na aplicação da posição clicada pelo cliente da vez
            OnStatusChanged(E);

            TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count];   // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
            TabelaUsuarios.Values.CopyTo(tcpClientes, 0);                    // Copia os objetos TcpClient no array
            for (int i = 0; i < tcpClientes.Length; i++)                     // Percorre a lista de clientes TCP
            {
                if (tcpClientes[i] == null) // Se a conexão for nula, segue para a próxima iteração...
                {
                    continue;
                }
                swEscritor = new StreamWriter(tcpClientes[i].GetStream());  // Envia a mensagem para o usuário atual no laço
                foreach (char letraUsada in palavra)
                {
                    foreach (string chave in LetrasSelecionadas.Keys)
                    {
                        if (letraUsada.Equals(LetrasSelecionadas[chave]))
                        {
                            swEscritor.WriteLine("11|" + chave + "|Y");   //Todas as letras que já foram usadas serão rasuradas
                            swEscritor.Flush();
                            break;
                        }
                    }
                }
                swEscritor.WriteLine("12|" + palavra);                      // Enviou a palavra, que será tratado por um método interno do cliente
                swEscritor.Flush();
                swEscritor.WriteLine("03|Admin: " + origem + " inseriu " + palavra); // Depois exibe a mensagem no log de um dos clientes selecionados, da posição clicada pelo jogador da vez
                swEscritor.Flush();
                swEscritor = null;
            }
            foreach (char letraUsada in palavra)
            {
                foreach (string chave in LetrasSelecionadas.Keys)
                {
                    if (letraUsada.Equals(LetrasSelecionadas[chave]))
                    {
                        Tabuleiro.Remove(chave);
                        LetrasSelecionadas.Remove(chave);
                        break;
                    }
                }
            }
        }

        // Envia mensagens de um usuário para todos os outros
        public static void EnviarQuantidadeJogadores() //O parâmetro mensagem vai ser tudo que vem após o primeiro pipe da mensagem superior (resposta do cliente)
        {
            StreamWriter swEscritor;

            TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count];   // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
            int quantidadeJogadores = 0;

            TabelaUsuarios.Values.CopyTo(tcpClientes, 0);
            quantidadeJogadores = tcpClientes.Length;

            for (int i = 0; i < tcpClientes.Length; i++)        // Percorre a lista de clientes TCP
            {
                if (tcpClientes[i] == null) // Se a conexão for nula, segue para a próxima iteração...
                {
                    continue;
                }
                swEscritor = new StreamWriter(tcpClientes[i].GetStream());  // Envia a mensagem para o usuário atual no laço
                swEscritor.WriteLine("17|" + quantidadeJogadores); // Poderia ser usuarios[posProxJogador], tanto faz.
                swEscritor.Flush();
                swEscritor = null;
            }
        }

        // Envia mensagens de um usuário para todos os outros
        public static void EnviarPrimeiroAJogar() //O parâmetro mensagem vai ser tudo que vem após o primeiro pipe da mensagem superior (resposta do cliente)
        {
            PartidaEmAndamento = true;
            StreamWriter swEscritor;

            //string[] usuarios = new string[TabelaUsuarios.Count];
            TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count];   // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
            int posJogadorSorteado = Rnd.Next(0, tcpClientes.Length);

            TabelaUsuarios.Values.CopyTo(tcpClientes, 0);

            E = new StatusChangedEventArgs(TabelaConexoes[tcpClientes[posJogadorSorteado]] + " sorteado para jogar!"); // Primeiro exibe a mensagem na aplicação da posição clicada pelo cliente da vez
            OnStatusChanged(E);

            for (int i = 0; i < tcpClientes.Length; i++)        // Percorre a lista de clientes TCP
            {
                if (tcpClientes[i] == null) // Se a conexão for nula, segue para a próxima iteração...
                {
                    continue;
                }
                swEscritor = new StreamWriter(tcpClientes[i].GetStream());  // Envia a mensagem para o usuário atual no laço
                swEscritor.WriteLine("18|" + TabelaConexoes[tcpClientes[posJogadorSorteado]]); // Poderia ser usuarios[posProxJogador], tanto faz.
                swEscritor.Flush();
                swEscritor.WriteLine("03|Admin: " + TabelaConexoes[tcpClientes[posJogadorSorteado]] + " sorteado para jogar!"); // Depois exibe a mensagem no log de um dos clientes selecionados, da posição clicada pelo jogador da vez
                swEscritor.Flush();
                swEscritor = null;
            }
        }

        // Envia mensagens de um usuário para todos os outros
        public static void EnviarProximoAJogar(string origem) //O parâmetro mensagem vai ser tudo que vem após o primeiro pipe da mensagem superior (resposta do cliente)
        {
            foreach (string chave in LetrasSelecionadas.Keys)
            {
                LetrasClicadas[chave] = false;
            }/*
            LetrasSelecionadas = new Dictionary<String, char>();*/

            StreamWriter swEscritor;

            int posProxJogador = 0; //MALAKOI

            E = new StatusChangedEventArgs(origem + " encerrou o turno."); // Primeiro exibe a mensagem na aplicação da posição clicada pelo cliente da vez
            OnStatusChanged(E);

            string[] usuarios = new string[TabelaUsuarios.Count];
            TabelaUsuarios.Keys.CopyTo(usuarios, 0);
            TcpClient[] tcpClientes = new TcpClient[TabelaUsuarios.Count]; // Cria um array de clientes TCPs do tamanho do numero de clientes existentes
            TabelaUsuarios.Values.CopyTo(tcpClientes, 0);                  // Copia os objetos TcpClient no array

            for (int i = 0; i < usuarios.Length; i++)                      // Percorre a lista de clientes TCP
            {
                if (usuarios[i].Equals(origem))
                {
                    if ((i + 1) < usuarios.Length) //O cliente que vem após o clienteAtual será o próximo a jogar
                    {
                        posProxJogador = i + 1;
                    }
                    else //Se o clienteAtual for a ultima posicao, então o cliente da primeira posição será o prox
                    {
                        posProxJogador = 0;
                    }
                    break;
                }
            }

            for (int i = 0; i < tcpClientes.Length; i++)        // Percorre a lista de clientes TCP
            {
                if (tcpClientes[i] == null) // Se a conexão for nula, segue para a próxima iteração...
                {
                    continue;
                }
                swEscritor = new StreamWriter(tcpClientes[i].GetStream());  // Envia a mensagem para o usuário atual no laço
                foreach (string chave in LetrasSelecionadas.Keys)
                {
                    swEscritor.WriteLine("11|" + chave + "|W");   //Todas as letras que sobraram serão viradas novamente, para o prox jogador poder jogar.
                    swEscritor.Flush();
                }
                swEscritor.WriteLine("19|" + usuarios[posProxJogador]);
                swEscritor.Flush();
                swEscritor.WriteLine("03|Admin: " + origem + " encerrou o turno."); // Depois exibe a mensagem no log de um dos clientes selecionados, da posição clicada pelo jogador da vez
                swEscritor.Flush();
                swEscritor.WriteLine("03|Admin: " + usuarios[posProxJogador] + " começou o turno!"); // Depois exibe a mensagem no log de um dos clientes selecionados, da posição clicada pelo jogador da vez
                swEscritor.Flush();
                swEscritor = null;
            }
            LetrasSelecionadas = new Dictionary<string,char>();
        }

        public void IniciarAtendimento()
        {
            try
            {

                // Pega o IP do primeiro dispostivo da rede
                IPAddress ipLocal = EnderecoIP;

                // Cria um objeto TCP listener usando o IP do servidor e porta definidas
                TcpLstCliente = new TcpListener(ipLocal, 2502);

                // Inicia o TCP listener e escuta as conexões
                TcpLstCliente.Start();

                // O laço While verifica se o servidor esta rodando antes de checar as conexões
                ServRodando = true;

                // Inicia uma nova tread que hospeda o listener
                ThreadListener = new Thread(ManterAtendimento);
                ThreadListener.Start();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void ManterAtendimento()
        {
            // Enquanto o servidor estiver rodando
            while (ServRodando == true)
            {
                // Aceita uma conexão pendente
                TcpCliente = TcpLstCliente.AcceptTcpClient();
                // Cria uma nova instância da conexão
                Conexao novaConexao = new Conexao(TcpCliente);
            }
        }

        public void inicializarTabuleiro()  //Preencherá a lista "Letras" com todas as letras que serão usadas
        {
            List<char> Letras = new List<char>();
            int letraPos = 0;
            String tabPos = "";
            //As variáveis represetam a quantidade dessas letras contidas na lista "Letras".
            int A = 7, //E = 7, O = 7.
                I = 6,
                U = 4, //S = 4
                L = 3, //R = 3.
                B = 2, //C = 2, D = 2, M = 2, N = 2, P = 2, T = 2, V = 2.
                F = 1; //G = 1, H = 1, J = 1, Q = 1, X = 1, Z = 1.
            //Adiciona as letras permitidas, à lista "Letras".
            for (int iteracao = 0; iteracao < 7; iteracao++)
            {
                if (iteracao < A)
                {
                    Letras.Add('A');
                    Letras.Add('E');
                    Letras.Add('O');
                }
                if (iteracao < I)
                {
                    Letras.Add('I');
                }
                if (iteracao < U)
                {
                    Letras.Add('U');
                    Letras.Add('S');
                }
                if (iteracao < L)
                {
                    Letras.Add('L');
                    Letras.Add('R');
                }
                if (iteracao < B)
                {
                    Letras.Add('B');
                    Letras.Add('C');
                    Letras.Add('D');
                    Letras.Add('M');
                    Letras.Add('N');
                    Letras.Add('P');
                    Letras.Add('T');
                    Letras.Add('V');
                }
                if (iteracao < F)
                {
                    Letras.Add('F');
                    Letras.Add('G');
                    Letras.Add('H');
                    Letras.Add('J');
                    Letras.Add('Q');
                    Letras.Add('X');
                    Letras.Add('Z');
                }
            }

            for (int i = 1; i <= 8; i++)    //Adiciona letras aleatórias, dentre as permitidas, ao dictionary "Tabuleiro".
            {
                for (int j = 1; j <= 8; j++)
                {
                    letraPos = Rnd.Next(0, Letras.Count - 1);   //Seleciona uma posição aleatória da lista de letras, que será adicionada ao nosso tabuleiro;
                    tabPos = i.ToString() + j.ToString();       //Variável criada para não precisarmos converter várias vezes o <i> e <j> para String;
                    Tabuleiro.Add(tabPos, Letras[letraPos]);    //Adiciona a letra selecionada da lista de letras ao nosso tabuleiro;
                    Letras.RemoveAt(letraPos);                  //Removeremos a letra desta posição da lista de letras, pois ela já foi usada.
                    LetrasClicadas.Add(tabPos, false);          //Setta pra false todo esse dictionary, que será usado como controle de pictureBoxes clicadas.
                }
            }
        }

        public static char virarLetra(string posicao) //Tenta virar uma letra na posicao escolhida, caso ela não tenha sido clicada ainda.
        {
            if (!LetrasClicadas[posicao])
            {
                if (ViradasPermitidas > 0)
                {
                    --ViradasPermitidas;
                    LetrasClicadas[posicao] = true;
                    LetrasSelecionadas.Add(posicao, Tabuleiro[posicao]);
                    return Tabuleiro[posicao];
                }
                else
                {
                    return 'W';    //Região do tabuleiro com letra ainda oculta.
                }
            }
            else if (Tabuleiro.ContainsKey(posicao))
            {
                return Tabuleiro[posicao];
            }
            else
            {
                return 'Y';     //Região do tabuleiro com letra descartada.
            }
        }

        public static string adicionarPalavra(string palavra) // -----------------
        {
            StringBuilder sb = new StringBuilder();

            foreach (char letraUsada in palavra)
            {
                foreach (string chave in LetrasSelecionadas.Keys)
                {
                    if (letraUsada.Equals(LetrasSelecionadas[chave]))
                    {
                        sb.Append(letraUsada);
                        Tabuleiro.Remove(chave);
                        LetrasSelecionadas.Remove(chave);
                        break;
                    }
                }
            }
            foreach (String chave in LetrasSelecionadas.Keys)
            {
                LetrasClicadas[chave] = false;
            }
            return sb.ToString();
        }
    }


    //CLASSE 3

    class Conexao   // Esta classe trata as conexões, serão tantas quanto as instâncias do usuários conectados
    {
        //VARIÁVEIS

        TcpClient TcpCliente;
        private Thread _threadSender;   // A thread que ira enviar a informação para o cliente
        private StreamReader _srReceptor;
        private StreamWriter _swEnviador;
        private string _usuarioAtual;
        private string _resposta;

        public Thread ThreadSender
        {
            get { return _threadSender; }
            set { _threadSender = value; }
        }

        public StreamReader SrReceptor
        {
            get { return _srReceptor; }
            set { _srReceptor = value; }
        }

        public StreamWriter SwEnviador
        {
            get { return _swEnviador; }
            set { _swEnviador = value; }
        }

        public string UsuarioAtual
        {
            get { return _usuarioAtual; }
            set { _usuarioAtual = value; }
        }

        public string Resposta
        {
            get { return _resposta; }
            set { _resposta = value; }
        }

        //CONSTRUTOR

        public Conexao(TcpClient socketCliente) // O construtor da classe que que toma a conexão TCP
        {
            TcpCliente = socketCliente;
            ThreadSender = new Thread(AceitarCliente);  // A thread que aceita o cliente e espera a mensagem
            ThreadSender.Start();  // A thread chama o método AceitaCliente()
        }

        //MÉTODOS

        private void AceitarCliente() // Ocorre quando um novo cliente é aceito
        {
            SrReceptor = new StreamReader(TcpCliente.GetStream());
            SwEnviador = new StreamWriter(TcpCliente.GetStream());

            UsuarioAtual = SrReceptor.ReadLine(); // Lê a informação da conta do cliente

            if (UsuarioAtual != "") // temos uma resposta do cliente
            {
                if (CServidor.TabelaUsuarios.Contains(UsuarioAtual) == true) // Armazena o nome do usuário na hash table
                {
                    SwEnviador.WriteLine("00|Este nome de usuário já existe."); // 00 => significa não conectado
                    SwEnviador.Flush();
                    FechaConexao();
                    return;
                }
                else if (UsuarioAtual == "Administrator")
                {
                    SwEnviador.WriteLine("00|Este nome de usuário é reservado."); // 00 => não conectado
                    SwEnviador.Flush();
                    FechaConexao();
                    return;
                }
                if (CServidor.PartidaEmAndamento == true) // Armazena o nome do usuário na hash table
                {
                    SwEnviador.WriteLine("00|Já existe uma partida em andamento."); // 00 => significa não conectado
                    SwEnviador.Flush();
                    FechaConexao();
                    return;
                }
                else
                {
                    SwEnviador.WriteLine("01"); // 01 => conectou com sucesso
                    SwEnviador.Flush();

                    CServidor.IncluirUsuario(TcpCliente, UsuarioAtual); // Inclui o usuário na hash table e inicia a escuta de suas mensagens
                }
            }
            else
            {
                FechaConexao();
                return;
            }
            //CONTINUANDO...
            /*try
            {*/
            while ((Resposta = SrReceptor.ReadLine()) != "99") // Continua aguardando por uma mensagem do usuário
            {
                if (Resposta == null) // Se for inválido, remove-o
                {
                    //CServidor.RemoverUsuario(TcpCliente);
                }
                else if (Resposta.Substring(0, 2).Equals("02"))//msg
                {
                    CServidor.EnviarMensagem(UsuarioAtual, Resposta.Substring(3, Resposta.Length - 3)); // Envia a mensagem para todos os outros usuários
                }
                else if (Resposta.Substring(0, 2).Equals("10"))//resultado dos dados
                {
                    CServidor.EnviarMensagemResultadoDados(UsuarioAtual, Resposta.Substring(3, Resposta.Length - 3)); // Insere o número de viradas permitidas
                }
                else if (Resposta.Substring(0, 2).Equals("11"))//posicao
                {
                    CServidor.EnviarMensagemMovimento(UsuarioAtual, Resposta.Substring(3, Resposta.Length - 3)); // Envia a letra e posição clicada para todos os outros usuários
                }
                else if (Resposta.Substring(0, 2).Equals("12"))//palavra
                {
                    CServidor.EnviarMensagemPalavra(UsuarioAtual, Resposta.Substring(3, Resposta.Length - 3)); // Envia a palavra inserida para todos os outros usuários
                }
                else if (Resposta.Substring(0, 2).Equals("17"))//quantidade jogadores
                {
                    CServidor.EnviarQuantidadeJogadores();
                }
                else if (Resposta.Substring(0, 2).Equals("18"))//sortear jogador
                {
                    CServidor.EnviarPrimeiroAJogar();
                }
                else if (Resposta.Substring(0, 2).Equals("19"))//fim de jogada
                {
                    CServidor.EnviarProximoAJogar(UsuarioAtual);
                }
            }
            /*}
            catch
            {
                CServidor.RemoverUsuario(TcpCliente); // Se houve um problema com este usuário desconecta-o
            }*/
        }

        private void FechaConexao()
        {
            // Fecha os objetos abertos
            TcpCliente.Close();
            SrReceptor.Close();
            SwEnviador.Close();
        }
    }
}
