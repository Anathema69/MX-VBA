# Manual de Instalación - Sistema de Gestión de Proyectos

**Versión:** 1.0.0
**Fecha:** 14 de octubre de 2025
**Desarrollado por:** Zuri Dev
**Soporte:** WhatsApp o Workana

---

## 📋 Requisitos del Sistema

- **Sistema Operativo:** Windows 10 o Windows 11
- **Espacio en disco:** Mínimo 500 MB disponibles
- **RAM:** Mínimo 4 GB recomendado
- **Conexión a Internet:** Requerida para acceso a la base de datos

> **Nota:** NO es necesario instalar .NET Framework o .NET 8. El instalador incluye todos los componentes necesarios.

---

## 🚀 Instalación

### Paso 1: Ejecutar el Instalador

1. Localiza el archivo **`SistemaGestionProyectosSetup.exe`**
2. Haz doble clic en el archivo
3. Si Windows muestra un mensaje de seguridad, haz clic en **"Más información"** y luego en **"Ejecutar de todos modos"**

### Paso 2: Seguir el Asistente de Instalación

1. **Pantalla de Bienvenida:**
   - Haz clic en **"Siguiente"**

2. **Carpeta de Destino:**
   - Por defecto se instala en: `C:\Program Files\SistemaGestionProyectos`
   - Puedes cambiar la ubicación si lo deseas
   - Haz clic en **"Siguiente"**

3. **Accesos Directos:**
   - ✅ Marca **"Crear acceso directo en el escritorio"** (recomendado)
   - Haz clic en **"Siguiente"**

4. **Confirmar Instalación:**
   - Revisa la configuración
   - Haz clic en **"Instalar"**

5. **Finalizar:**
   - ✅ Marca **"Ejecutar aplicación"** para iniciar el sistema
   - Haz clic en **"Finalizar"**

---

## 🔐 Primer Inicio de Sesión

1. Al abrir la aplicación, verás la pantalla de **Login**
2. Ingresa las **credenciales** que te proporcioné en el archivo adjunto
3. Haz clic en **"Iniciar Sesión"**

> **Importante:** Las credenciales se enviaron en un documento separado por seguridad.

---

## ✅ Pruebas Básicas Recomendadas

### 1. Verificar Navegación

- [ ] Desde el menú principal, navega entre los diferentes módulos:
  - Clientes
  - Órdenes
  - Proveedores
  - Nómina
  - Reportes

### 2. Crear un Cliente de Prueba

- [ ] Ve a **"Gestión de Clientes"**
- [ ] Haz clic en **"Nuevo Cliente"**
- [ ] Completa los datos básicos (nombre, contacto)
- [ ] Guarda el cliente
- [ ] Verifica que aparezca en la lista

### 3. Probar Sesión de Inactividad (Opcional)

- [ ] Deja la aplicación abierta sin interactuar durante **25 minutos**
- [ ] A los **25 minutos**, verás un **banner de advertencia** indicando que la sesión cerrará en 5 minutos
- [ ] Puedes hacer clic en **"Extender sesión"** o simplemente mover el mouse para continuar trabajando
- [ ] Si no hay actividad durante **30 minutos** total, la sesión se cerrará automáticamente

### 4. Verificar Logs (Para diagnóstico)

Los logs del sistema se guardan automáticamente en:
```
C:\Users\{tu_usuario}\AppData\Local\SistemaGestionProyectos\logs\
```

Para acceder rápidamente:
1. Presiona `Win + R`
2. Escribe: `%LocalAppData%\SistemaGestionProyectos\logs`
3. Presiona Enter

---

## 🔄 Instalación en Múltiples Equipos

Para instalar en varios equipos:

1. **Copia el instalador** `SistemaGestionProyectosSetup.exe` a cada equipo
2. **Ejecuta la instalación** siguiendo los pasos anteriores en cada PC
3. **Usa las mismas credenciales** en todos los equipos
4. **Cada usuario** puede iniciar sesión simultáneamente desde diferentes equipos

> **Importante:** La aplicación permite acceso concurrente de múltiples usuarios. Todos trabajarán sobre la misma base de datos en tiempo real.

---

## ⚠️ Problemas Comunes

### La aplicación no inicia

1. Verifica que tienes **Windows 10 o 11**
2. Asegúrate de que tu **antivirus no está bloqueando** la aplicación
3. Ejecuta el instalador como **Administrador** (clic derecho → "Ejecutar como administrador")

### No puedo iniciar sesión

1. Verifica que las **credenciales sean correctas** (distinguen mayúsculas/minúsculas)
2. Confirma que tienes **conexión a Internet activa**
3. Si el problema persiste, contacta soporte

### La aplicación se cierra sola

- Esto ocurre después de **30 minutos de inactividad** por seguridad
- Es un comportamiento normal del sistema
- Simplemente vuelve a iniciar sesión

---

## 📞 Soporte Técnico

Si tienes dudas o problemas durante la instalación o uso del sistema:

- **WhatsApp:** Contacta por el mismo número donde me escribes
- **Workana:** Envía un mensaje a través de la plataforma
- **Respuesta:** Te atenderé lo antes posible

---

## 📁 Archivos Incluidos

- `SistemaGestionProyectosSetup.exe` - Instalador principal
- `CREDENCIALES.txt` (documento separado) - Usuario y contraseña inicial
- `MANUAL_INSTALACION.md` - Este documento

---

## 🔐 Seguridad

- ✅ Todas las contraseñas se almacenan encriptadas
- ✅ La conexión a la base de datos usa protocolos seguros (HTTPS)
- ✅ Sesión automática por inactividad después de 30 minutos
- ✅ Los logs se guardan localmente en cada equipo para auditoría

---

## 📝 Notas Finales

- La aplicación se actualiza automáticamente conectándose a la base de datos en la nube
- No es necesario hacer backups locales, toda la información se guarda en la nube
- Puedes desinstalar desde **Panel de Control → Programas y características**

---

**¡Gracias por confiar en Zuri Dev!**

*Si tienes sugerencias de mejora o necesitas funcionalidades adicionales, no dudes en contactarme.*
