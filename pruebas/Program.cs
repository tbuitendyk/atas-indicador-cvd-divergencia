// Pruebas de comportamiento de los cinco indicadores, sin ATAS. Devuelve el número de fallos (0 = todo bien).

var c = new Comprobador();

PruebasTendencia.Ejecutar(c);
PruebasCvd.Ejecutar(c);
PruebasFootprint.Ejecutar(c);
PruebasLvn.Ejecutar(c);
PruebasCombinado.Ejecutar(c);

return c.Resumen();
