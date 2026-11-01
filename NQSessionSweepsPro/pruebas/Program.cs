// Pruebas de NQSessionSweepsPro sin ATAS ni Windows. Devuelve el número de fallos (0 = todo bien).
var c = new Comprobador();

PruebasSesiones.Ejecutar(c);
PruebasBarridos.Ejecutar(c);

return c.Resumen();
