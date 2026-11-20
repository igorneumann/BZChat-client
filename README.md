# Bezares Chat: cliente de Windows

Programa **portable** (un único `BezaresChat.exe` de unos 72 KB, sin instalación) con el que la recepcionista y los comerciales contestan los chats de la web, WhatsApp y Facebook de [Bezares Chat](https://github.com/igorneumann/Bezares-chat).

## Uso

1. Copia `release/BezaresChat.exe` donde quieras (escritorio, carpeta de red, USB…) y ábrelo.
2. Introduce el servidor (`https://chat.bezares.com`), tu correo y tu contraseña. El administrador crea los usuarios en `https://chat.bezares.com/admin`.
   - **Mostrar contraseña**: casilla para ver lo que escribes.
   - **Recordar sesión**: si la desmarcas, no se guarda la sesión y tendrás que entrar cada vez que abras el programa.
   - **Iniciar con Windows**: el programa se abre solo al encender el ordenador, minimizado en la bandeja, y avisa igualmente de los chats. También se activa o desactiva desde el menú del icono de la bandeja.
   - **¿Has olvidado tu contraseña?**: abre la página para recibir un enlace de recuperación por correo.
3. Funciones:
   - **Contestar chats**: escribe y pulsa *Intro* (Mayús+Intro para una línea nueva).
   - **📎 Adjuntar archivo**: imágenes, audio, vídeo y documentos (PDF, Word, Excel, PowerPoint, OpenDocument, RTF, TXT, CSV). También se pueden **arrastrar** sobre la conversación. El tamaño máximo lo fija el administrador (16 MB por defecto).
   - **↪ Transferir**: solo para recepción. Pasa el chat a un comercial con una nota opcional.
   - **📋 Copiar**: debajo de cada mensaje, copia su texto (o el enlace del adjunto) al portapapeles.
   - **✉ Correo**: envía la transcripción completa de la conversación por correo electrónico. Por defecto, al correo del cliente; se puede cambiar antes de enviar.
   - **🗑 Borrar** (recepción y administración): borra definitivamente la conversación, con sus mensajes y adjuntos, para todo el equipo, tras pedir confirmación. Si el visitante web vuelve a escribir, empieza un chat nuevo.
   - **📝 Plantillas** (solo recepción): menú con respuestas preparadas que se insertan en el cuadro de texto. Desde «Gestionar plantillas…» se crean, editan y borran; se comparten entre todas las recepcionistas. Variables: `{nombre}` (cliente) y `{agente}` (tu nombre).
4. **Aviso de llamada muy visible** cuando entra un chat:
   - **timbre de teléfono** en bucle hasta que alguien lo atiende;
   - ventana roja y naranja **parpadeando, siempre encima**, con el botón **📞 ATENDER**;
   - botón de la barra de tareas e icono de la bandeja parpadeando.
   - «Posponer 2 minutos» silencia el aviso temporalmente; un chat nuevo lo vuelve a activar.
5. Cerrar la ventana **no** cierra el programa: sigue en la bandeja del sistema (junto al reloj) para no perder avisos. Para salir: clic derecho en el icono → *Salir*. Desde ese menú también se silencia el sonido.

Los mensajes en otros idiomas aparecen **traducidos al español** con el original debajo, y tus respuestas se envían traducidas al idioma del cliente.

## Datos que guarda

Junto al `.exe` crea `BezaresChat.ini` con el servidor, el correo y el token de sesión **cifrado con DPAPI** (solo lo puede leer tu usuario de Windows). No guarda la contraseña. «Iniciar con Windows» añade una entrada en el registro del usuario actual (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), sin necesitar permisos de administrador. Si mueves el `.exe`, la ruta se actualiza al abrirlo. Si algo falla, escribe los detalles en `BezaresChat-errores.log`.

## Requisitos

Windows 10 u 11 (incluyen .NET Framework 4.8). No necesita instalar nada.

## Compilar

Doble clic en `build.bat`. Usa el compilador de C# que ya trae Windows y genera `BezaresChat.exe` en unos segundos.

## Seguridad

- Solo se conecta por **HTTPS** (admite `http://localhost` únicamente para pruebas).
- No interpreta HTML: los mensajes se muestran como texto y solo abre enlaces `http(s)` en el navegador.
- Recomendado: firmar el `.exe` con un certificado de firma de código para evitar el aviso de SmartScreen.
