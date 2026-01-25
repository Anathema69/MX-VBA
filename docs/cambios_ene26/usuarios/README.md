# Módulo Gestión de Usuarios

**Versión:** 1.0
**Fecha:** 25 de Enero 2026
**Estado:** Funcional - Listo para producción

---

## 1. Descripción

Sistema CRUD completo para gestión de usuarios del sistema:
- **Crear usuarios** con validación de datos
- **Editar usuarios** (nombre, email, rol, estado)
- **Cambiar contraseñas** con hash BCrypt
- **Activar/Desactivar usuarios** (soft delete)
- **Filtrar** por rol, búsqueda de texto, activos/inactivos

---

## 2. Estructura de Base de Datos

### 2.1 Tabla `users`

| Columna | Tipo | Nullable | Descripción |
|---------|------|----------|-------------|
| `id` | integer | NO | PK autoincremental |
| `username` | varchar | NO | Usuario de login (único) |
| `email` | varchar | NO | Correo electrónico |
| `password_hash` | varchar | NO | Hash BCrypt de la contraseña |
| `full_name` | varchar | NO | Nombre completo |
| `role` | varchar | NO | Rol del usuario |
| `is_active` | boolean | SI | Estado (default: true) |
| `last_login` | timestamp | SI | Último acceso |
| `created_at` | timestamp | SI | Fecha de creación |
| `updated_at` | timestamp | SI | Última actualización |

### 2.2 Roles Disponibles

| Rol | Descripción | Acceso |
|-----|-------------|--------|
| `direccion` | Dirección General | Completo (todos los módulos) |
| `administracion` | Administración | Órdenes, Balance, Calendario |
| `proyectos` | Proyectos | Órdenes |
| `coordinacion` | Coordinación | Órdenes |
| `ventas` | Ventas | Portal del Vendedor |

---

## 3. Permisos de Acceso

Solo los usuarios con rol `direccion` pueden acceder al módulo de gestión de usuarios.

| Rol | Acceso al módulo |
|-----|------------------|
| direccion | ✓ Completo |
| administracion | ✗ Sin acceso |
| proyectos | ✗ Sin acceso |
| coordinacion | ✗ Sin acceso |
| ventas | ✗ Sin acceso |

---

## 4. Archivos del Módulo

### 4.1 Modelo

**`Models/Database/UserDb.cs`**

```csharp
[Table("users")]
public class UserDb : BaseModel
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string FullName { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLogin { get; set; }
}
```

### 4.2 Servicio

**`Services/Users/UserService.cs`**

| Método | Descripción |
|--------|-------------|
| `AuthenticateUser(username, password)` | Login con BCrypt |
| `GetAllUsers()` | Obtener todos los usuarios |
| `GetActiveUsers()` | Solo usuarios activos |
| `GetUserById(id)` | Usuario por ID |
| `GetUserByUsername(username)` | Usuario por username |
| `GetUsersByRole(role)` | Usuarios por rol |
| `CreateUser(user, plainPassword)` | Crear usuario con hash |
| `UpdateUser(user)` | Actualizar datos |
| `ChangePassword(userId, newPassword)` | Cambiar contraseña |
| `DeactivateUser(userId)` | Desactivar (soft delete) |
| `ReactivateUser(userId)` | Reactivar usuario |
| `DeleteUser(userId)` | Eliminar permanentemente |
| `UserExists(username)` | Verificar si existe |
| `EmailExists(email)` | Verificar email duplicado |

### 4.3 Vista

**`Views/UserManagementWindow.xaml`** + **`.xaml.cs`**

- Interfaz moderna con tarjetas de usuario
- Búsqueda en tiempo real
- Filtro por rol (ComboBox)
- Checkbox para mostrar/ocultar inactivos
- Badges con contadores (total, activos, inactivos)
- Diálogos modales para crear/editar/cambiar contraseña

---

## 5. Interfaz de Usuario

### 5.1 Lista de Usuarios

```
┌────────────────────────────────────────────────────────────┐
│  GESTIÓN DE USUARIOS                           [← Volver] │
│  [5 usuarios] [3 activos] [2 inactivos]                    │
├────────────────────────────────────────────────────────────┤
│  [🔍 Buscar...]  [Todos los roles ▼]  ☐ Mostrar inactivos  │
│                                            [➕ Nuevo Usuario]│
├────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────────┐  │
│  │ [JG] Juan García           juan@empresa.com          │  │
│  │      @juangarcia           Último: 25/01/26 10:30    │  │
│  │                    [DIRECCIÓN] [ACTIVO]  [✏️][🔑][🚫] │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ [ML] María López           maria@empresa.com         │  │
│  │      @marialopez           Último: 24/01/26 18:45    │  │
│  │                    [VENTAS] [ACTIVO]     [✏️][🔑][🚫] │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
```

### 5.2 Colores por Rol

| Rol | Color Fondo | Color Texto |
|-----|-------------|-------------|
| direccion | Amarillo claro | Naranja |
| administracion | Azul claro | Azul |
| proyectos | Verde claro | Verde |
| coordinacion | Índigo claro | Índigo |
| ventas | Rosa claro | Rosa |

### 5.3 Diálogo Crear/Editar Usuario

```
┌────────────────────────────────┐
│  Nuevo Usuario                 │
├────────────────────────────────┤
│  Usuario *                     │
│  [____________________]        │
│                                │
│  Nombre completo *             │
│  [____________________]        │
│                                │
│  Email *                       │
│  [____________________]        │
│                                │
│  Rol *                         │
│  [Ventas            ▼]         │
│                                │
│  Contraseña *                  │
│  [____________________]        │
│                                │
│  Confirmar contraseña *        │
│  [____________________]        │
│                                │
│  ☑ Usuario activo              │
│                                │
│        [Cancelar] [Crear]      │
└────────────────────────────────┘
```

### 5.4 Diálogo Cambiar Contraseña

```
┌────────────────────────────────┐
│  Cambiar Contraseña            │
│  Usuario: @juangarcia          │
├────────────────────────────────┤
│  Nueva contraseña *            │
│  [____________________]        │
│                                │
│  Confirmar contraseña *        │
│  [____________________]        │
│                                │
│        [Cancelar] [Cambiar]    │
└────────────────────────────────┘
```

---

## 6. Seguridad

### 6.1 Hash de Contraseñas

Se utiliza **BCrypt.Net** para el hash de contraseñas:

```csharp
// Crear hash
string hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

// Verificar
bool isValid = BCrypt.Net.BCrypt.Verify(password, hash);
```

### 6.2 Validaciones

- Username: único, requerido
- Email: formato válido, requerido
- Contraseña: mínimo 6 caracteres
- Rol: debe ser uno de los definidos

---

## 7. Uso

### 7.1 Acceso desde Menú Principal

1. Iniciar sesión con usuario rol `direccion`
2. En el menú principal, hacer clic en "PORTAL USUARIOS"
3. Se abre la ventana de gestión

### 7.2 Crear Usuario

1. Clic en "➕ Nuevo Usuario"
2. Llenar todos los campos requeridos
3. Seleccionar rol
4. Establecer contraseña
5. Clic en "Crear Usuario"

### 7.3 Editar Usuario

1. Clic en el ícono ✏️ del usuario
2. Modificar campos necesarios
3. Clic en "Guardar"

### 7.4 Cambiar Contraseña

1. Clic en el ícono 🔑 del usuario
2. Ingresar nueva contraseña
3. Confirmar contraseña
4. Clic en "Cambiar"

### 7.5 Desactivar/Reactivar Usuario

1. Clic en el ícono 🚫 (desactivar) o ✅ (reactivar)
2. Confirmar acción

---

## 8. Historial de Cambios

### v1.0 (25/01/2026)
- Versión inicial
- CRUD completo de usuarios
- Interfaz con tarjetas y filtros
- Hash BCrypt para contraseñas
- Validación de datos
- Integración con menú principal

---

## 9. Próximos Pasos

- [ ] Agregar auditoría de cambios
- [ ] Exportar lista de usuarios
- [ ] Historial de accesos por usuario
- [ ] Recuperación de contraseña por email
- [ ] Bloqueo por intentos fallidos

---

## 10. Contacto

**Desarrollado para:** IMA Mecatrónica
**Repositorio:** github.com/Anathema69/MX-VBA
