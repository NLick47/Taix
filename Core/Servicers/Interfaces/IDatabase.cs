using Core.Librarys.SQLite;

namespace Core.Servicers.Interfaces;

public interface IDatabase
{
    TaiDbContext GetReaderContext();
    //void CloseReader();

    void CloseWriter();
}