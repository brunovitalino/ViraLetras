using System;
using System.Windows.Forms;
using System.Net;

using CamadaControl;

namespace CamadaView
{
    public partial class FrmServidor : Form
    {
        //VARIÁVEIS


        //DELEGATES
        private delegate void AtualizaLogCallback(string strMensagem);

        //CONSTRUTOR
        public FrmServidor()
        {
            InitializeComponent();
        }

        //MÉTODOS
        private void IniciarAtendimento()
        {
            if (txtIP.Text.Equals(string.Empty))
            {
                MessageBox.Show("Informe o endereço IP.");
                txtIP.Focus();
                return;
            }
            try
            {
                IPAddress enderecoIP = IPAddress.Parse(txtIP.Text);     // Analisa o endereço IP do servidor informado no textbox
                CServidor nossoServidor = new CServidor(enderecoIP);    // Cria uma nova instância do objeto ChatServidor
                CServidor.StatusChanged += new StatusChangedEventHandler(nossoServidor_StatusChanged); // Vincula o tratamento de evento StatusChanged à nossoServidor_StatusChanged
                nossoServidor.inicializarTabuleiro();                   // Insere as letras no tableiro;
                nossoServidor.IniciarAtendimento();                     // Inicia o atendimento das conexões
                txtLog.AppendText("Monitorando as conexões...\r\n");    // Mostra que nos iniciamos o atendimento para conexões
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro de conexão : " + ex.Message);
            }
        }

        private void AtualizaLog(string mensagem)
        {
            txtLog.AppendText(mensagem + "\r\n"); //Atualiza o log de mensagens
        }

        //EVENTOS
        public void nossoServidor_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            this.Invoke(new AtualizaLogCallback(this.AtualizaLog), new object[] { e.MensagemEvento }); // Chama o método que atualiza o formulário
        }

        private void FrmServidor_Load(object sender, EventArgs e)
        {
            this.Top = 0;
            this.Left = 0;
            txtIP.Select();
        }

        private void btnAtender_Click(object sender, System.EventArgs e)
        {
            IniciarAtendimento();
        }

        private void txtIP_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13) // Se pressionou a tecla Enter, então faça...
            {
                IniciarAtendimento();
            }
        }
    }
}

