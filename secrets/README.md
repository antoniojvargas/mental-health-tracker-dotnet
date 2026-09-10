# Secrets

Este directorio NO debe contener secretos reales. Solo documentación o plantillas.

Los archivos de secretos están excluidos del repositorio mediante `.gitignore`
(`secrets/*.txt` y `.env`), por lo que cualquier archivo aquí con extensiones o
nombres sensibles será ignorado por git.

## Gestión de secretos recomendada

- **Local**: user-secrets de .NET o variables de entorno
- **Entornos**: variables de entorno en el runner de CI/CD o un gestor de secretos en la nube
- **Nunca** versionar contraseñas, tokens o connection strings con datos reales

## Plantillas permitidas

Se pueden versionar archivos de ejemplo (p. ej. `secrets/connection-strings.example.txt`)
con valores ficticios, siempre que no contengan información real.

Nunca agregar secretos reales a este directorio.