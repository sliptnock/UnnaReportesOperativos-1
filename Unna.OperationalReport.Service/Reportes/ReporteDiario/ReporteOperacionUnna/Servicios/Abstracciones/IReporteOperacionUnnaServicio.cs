﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaCnpc.Dtos;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.ReporteOperacionUnna.Dtos;
using Unna.OperationalReport.Tools.Comunes.Infraestructura.Dtos;

namespace Unna.OperationalReport.Service.Reportes.ReporteDiario.ReporteOperacionUnna.Servicios.Abstracciones
{
    public interface IReporteOperacionUnnaServicio
    {
        Task<OperacionDto<ReporteOperacionUnnaDto>> ObtenerAsync(long idUsuario);
        Task<OperacionDto<RespuestaSimpleDto<bool>>> GuardarAsync(ReporteOperacionUnnaDto peticion);
    }
}
