WPF – Sistema de gestión e impresión de etiquetas con BarTender
Una aplicación empresarial WPF desarrollada en C#/.NET para gestionar de forma centralizada la selección, configuración e impresión de etiquetas mediante integración con BarTender y servicios backend.
El objetivo del proyecto fue desacoplar la aplicación de escritorio de la lógica y configuración de negocio. La interfaz WPF trabaja con los datos necesarios para la operativa del usuario, mientras que la información de productos, configuraciones de etiquetas y plantillas se obtiene a través de una API conectada al sistema de gestión.
Arquitectura
┌─────────────────────────────┐
│         WPF Client          │
│       C# / XAML / .NET      │
└──────────────┬──────────────┘
               │
               │ NSwag generated client
               │ REST / DTOs
               ▼
┌─────────────────────────────┐
│        Backend API          │
│          .NET / C#          │
└──────────────┬──────────────┘
               │
               │ consulta de configuración
               ▼
┌─────────────────────────────┐
│      Sistema de gestión     │
│                             │
│ Productos · Etiquetas       │
│ Plantillas · Configuración  │
└──────────────┬──────────────┘
               │
               │ datos resueltos
               ▼
┌─────────────────────────────┐
│      BarTender Service      │
│                             │
│ Generación / impresión      │
│       de etiquetas          │
└─────────────────────────────┘
Flujo funcional
El usuario trabaja desde la aplicación WPF.
Selecciona/introduce el producto sobre el que quiere realizar la operación.
El cliente consulta el backend mediante la API.
El backend obtiene del sistema de gestión la configuración asociada al producto.
Se recuperan los datos necesarios para determinar la etiqueta y plantilla correspondiente.
La aplicación utiliza dicha configuración para realizar la operación contra BarTender.
BarTender procesa la plantilla y ejecuta la generación/impresión correspondiente.
Decisiones técnicas
Separación entre presentación, integración y datos.
WPF como cliente de escritorio.
Backend desacoplado para centralizar reglas e información.
Comunicación mediante API.
Cliente y modelos generados mediante NSwag/OpenAPI, reduciendo duplicación y manteniendo sincronizado el contrato cliente-servidor.
Integración con BarTender Web Service.
Persistencia/configuración centralizada en base de datos.
CI/CD mediante Azure Pipelines — de hecho tu repo tiene azure-pipelines.yml. �
GitHub
Stack
C# · .NET · WPF · XAML · REST API · NSwag/OpenAPI · SQL · BarTender · Azure DevOps / Pipelines
