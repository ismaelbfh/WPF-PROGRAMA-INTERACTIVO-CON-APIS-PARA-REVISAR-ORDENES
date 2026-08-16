namespace APP.GK.WPF.Modelos
{
    /// <summary>
    /// Representa el endpoint TCP de una cámara/lector.
    /// Se usa para:
    /// - hacer ping
    /// - enviar comandos
    /// - abrir escucha TCP
    /// </summary>
    public class CameraEndpoint
    {
        public string Ip { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;

        /// <summary>
        /// Nombre lógico opcional para facilitar mensajes y depuración.
        /// Ejemplo:
        /// - CamaraPrincipal
        /// - Camara2
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}